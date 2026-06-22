using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ElasticBreath.App.Interop;

namespace ElasticBreath.App.UI;

public enum EdgeOverlayState
{
    Hidden,
    Warning,
    Hard,
    RestBase,
    RestElastic,
    RestOvertime,
    Paused
}

/// <summary>
/// 边缘光晕覆盖层，使用 Win32 UpdateLayeredWindow 直接渲染像素。
/// 完全绕过 WPF 渲染管线，GPU 占用极低。
/// </summary>
public partial class EdgeOverlayWindow : Window
{
    /* ---- 动画与置顶定时器 ---- */
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _reTopmostTimer;
    private bool _reTopmostEnabled;
    private int _reTopmostIntervalSeconds = 5;

    /* ---- 渲染状态 ---- */
    private DateTime _animationStartUtc = DateTime.UtcNow;
    private double _baseOpacity = 0.3;
    private int _glowThickness = 80;
    private EdgeOverlayState _state = EdgeOverlayState.Hidden;
    private bool _enableEdgeGlow = true;
    private bool _showTopProgress;
    private System.Drawing.Rectangle _boundsPx = new(0, 0, 1, 1);
    private double _topProgressRatio;

    /* ---- 原生 Layered Window 资源 ---- */
    private IntPtr _hwnd;
    private IntPtr _hBitmap;
    private IntPtr _pixels;       // DIB Section 像素内存指针
    private int _bitmapWidth;
    private int _bitmapHeight;
    private bool _bitmapDirty = true; // 标记是否需要重新分配位图

    /* ---- 缓存上一次渲染的颜色，避免无变化时重复渲染 ---- */
    private byte _lastR, _lastG, _lastB, _lastA;

    public EdgeOverlayWindow()
    {
        InitializeComponent();

        /* 动画定时器，200ms 间隔 */
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _animationTimer.Tick += (_, _) => RenderFrame();

        /* 周期性重新置顶定时器 */
        _reTopmostTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _reTopmostTimer.Tick += (_, _) =>
        {
            if (_hwnd != IntPtr.Zero && IsVisible)
                Win32Native.ForceTopmost(_hwnd);
        };

        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        /* 设置为 Layered Window，禁用 WPF 自身渲染 */
        Win32Native.SetLayered(_hwnd);
    }

    /// <summary>配置周期性重新置顶功能</summary>
    public void ConfigureReTopmost(bool enabled, int intervalSeconds)
    {
        intervalSeconds = Math.Max(1, intervalSeconds);
        if (_reTopmostEnabled == enabled && _reTopmostIntervalSeconds == intervalSeconds)
            return;
        _reTopmostEnabled = enabled;
        _reTopmostIntervalSeconds = intervalSeconds;
        _reTopmostTimer.Stop();
        if (enabled)
        {
            _reTopmostTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
            _reTopmostTimer.Start();
        }
    }

    public void SetBounds(System.Drawing.Rectangle bounds)
    {
        if (_boundsPx != bounds)
        {
            _boundsPx = bounds;
            _bitmapDirty = true;
        }
    }

    public void UpdateOverlay(
        EdgeOverlayState state,
        bool enableEdgeGlow,
        bool showTopProgress,
        double topProgressRatio,
        int glowThickness,
        double baseOpacity,
        bool hideAll)
    {
        _state = state;
        _enableEdgeGlow = enableEdgeGlow;
        _showTopProgress = showTopProgress;
        _glowThickness = glowThickness;
        _baseOpacity = baseOpacity;
        _topProgressRatio = topProgressRatio;
        _animationStartUtc = DateTime.UtcNow;

        var shouldShowWindow = !hideAll && ((enableEdgeGlow && state != EdgeOverlayState.Hidden) || showTopProgress);
        if (!shouldShowWindow)
        {
            Hide();
            _animationTimer.Stop();
            return;
        }

        /* 根据是否需要动画来启停定时器 */
        var needsAnimation = NeedsAnimation(state);
        if (needsAnimation && !_animationTimer.IsEnabled)
            _animationTimer.Start();
        else if (!needsAnimation && _animationTimer.IsEnabled)
            _animationTimer.Stop();

        if (!IsVisible)
            Show();

        /* 立即渲染一帧 */
        RenderFrame();
    }

    /// <summary>判断当前状态是否需要动画（脉冲/闪烁）</summary>
    private static bool NeedsAnimation(EdgeOverlayState state)
    {
        return state is EdgeOverlayState.Warning
            or EdgeOverlayState.Hard
            or EdgeOverlayState.RestBase
            or EdgeOverlayState.RestElastic
            or EdgeOverlayState.RestOvertime;
    }

    private void RenderFrame()
    {
        if (_hwnd == IntPtr.Zero || !IsVisible)
            return;

        ResolveVisual(out var baseR, out var baseG, out var baseB, out var alphaFactor, out var pulsePeriod, out var blink);

        /* 计算脉冲/闪烁后的最终权重 */
        var opacityFactor = alphaFactor;
        if (pulsePeriod > 0)
        {
            var phase = (DateTime.UtcNow - _animationStartUtc).TotalSeconds / pulsePeriod;
            var wave = 0.5 + (0.5 * Math.Sin(phase * Math.PI * 2));
            opacityFactor *= (0.35 + (0.65 * wave));
        }

        if (blink)
        {
            var step = (int)((DateTime.UtcNow - _animationStartUtc).TotalMilliseconds / 500) % 2;
            opacityFactor *= step == 0 ? 1 : 0.2;
        }

        var finalAlpha = (byte)Math.Clamp(_baseOpacity * opacityFactor * 255, 0, 255);

        /* 如果颜色和 alpha 都没变，跳过渲染 */
        if (!_bitmapDirty && finalAlpha == _lastA && baseR == _lastR && baseG == _lastG && baseB == _lastB)
            return;

        _lastA = finalAlpha;
        _lastR = baseR;
        _lastG = baseG;
        _lastB = baseB;

        /* 确保位图尺寸匹配 */
        EnsureBitmap();

        if (_hBitmap == IntPtr.Zero || _pixels == IntPtr.Zero)
            return;

        /* 定位窗口 */
        Win32Native.SetWindowBoundsPixels(_hwnd, _boundsPx);

        /* 填充像素 */
        FillPixels(baseR, baseG, baseB, finalAlpha);

        /* 提交到 Layered Window */
        Win32Native.RenderLayeredWindow(_hwnd, _hBitmap, _pixels, _bitmapWidth, _bitmapHeight);
    }

    /// <summary>确保 DIB Section 位图尺寸与屏幕匹配</summary>
    private void EnsureBitmap()
    {
        var w = _boundsPx.Width;
        var h = _boundsPx.Height;
        if (w <= 0 || h <= 0)
            return;

        if (!_bitmapDirty && _hBitmap != IntPtr.Zero && _bitmapWidth == w && _bitmapHeight == h)
            return;

        /* 释放旧位图 */
        if (_hBitmap != IntPtr.Zero)
        {
            Win32Native.DestroyGdiObject(_hBitmap);
            _hBitmap = IntPtr.Zero;
            _pixels = IntPtr.Zero;
        }

        _hBitmap = Win32Native.CreateArgbBitmap(w, h, out _pixels);
        _bitmapWidth = w;
        _bitmapHeight = h;
        _bitmapDirty = false;
    }

    /// <summary>在像素缓冲区中绘制边缘光晕和进度条</summary>
    private void FillPixels(byte r, byte g, byte b, byte alpha)
    {
        var w = _bitmapWidth;
        var h = _bitmapHeight;
        var stride = w * 4; // 每像素 4 字节 (BGRA)
        var totalBytes = stride * h;

        /* 清空为全透明 */
        unsafe
        {
            var ptr = (byte*)_pixels;
            for (var i = 0; i < totalBytes; i++)
                ptr[i] = 0;
        }

        if (alpha == 0)
            return;

        var t = _glowThickness;

        /* 绘制四条边缘的渐变光晕 */
        if (_enableEdgeGlow)
        {
            unsafe
            {
                var ptr = (byte*)_pixels;

                /* 上边缘：从上到下渐变，alpha 从 alpha 渐变到 0 */
                for (var y = 0; y < t && y < h; y++)
                {
                    var ratio = 1.0 - (double)y / t;
                    var a = (byte)(alpha * ratio);
                    var rowOffset = y * stride;
                    for (var x = 0; x < w; x++)
                    {
                        var offset = rowOffset + x * 4;
                        ptr[offset] = b;       // Blue
                        ptr[offset + 1] = g;   // Green
                        ptr[offset + 2] = r;   // Red
                        ptr[offset + 3] = a;   // Alpha
                    }
                }

                /* 下边缘：从下到上渐变 */
                for (var y = Math.Max(0, h - t); y < h; y++)
                {
                    var ratio = (double)(y - (h - t)) / t;
                    var a = (byte)(alpha * ratio);
                    var rowOffset = y * stride;
                    for (var x = 0; x < w; x++)
                    {
                        var offset = rowOffset + x * 4;
                        ptr[offset] = b;
                        ptr[offset + 1] = g;
                        ptr[offset + 2] = r;
                        ptr[offset + 3] = a;
                    }
                }

                /* 左边缘：从左到右渐变 */
                for (var y = 0; y < h; y++)
                {
                    var rowOffset = y * stride;
                    for (var x = 0; x < t && x < w; x++)
                    {
                        var ratio = 1.0 - (double)x / t;
                        var a = (byte)(alpha * ratio);
                        var offset = rowOffset + x * 4;
                        /* 与已有像素做 alpha 混合（叠加） */
                        BlendPixel(ptr, offset, r, g, b, a);
                    }
                }

                /* 右边缘：从右到左渐变 */
                for (var y = 0; y < h; y++)
                {
                    var rowOffset = y * stride;
                    for (var x = Math.Max(0, w - t); x < w; x++)
                    {
                        var ratio = (double)(x - (w - t)) / t;
                        var a = (byte)(alpha * ratio);
                        var offset = rowOffset + x * 4;
                        BlendPixel(ptr, offset, r, g, b, a);
                    }
                }
            }
        }

        /* 绘制顶部进度条 */
        if (_showTopProgress)
        {
            DrawProgressBar(r, g, b, alpha);
        }
    }

    /// <summary>在像素缓冲区中绘制顶部进度条</summary>
    private void DrawProgressBar(byte r, byte g, byte b, byte alpha)
    {
        var w = _bitmapWidth;
        var barWidth = 360;
        var barHeight = 10;
        var barX = (w - barWidth) / 2;
        var barY = 8;

        unsafe
        {
            var ptr = (byte*)_pixels;

            /* 背景：半透明深色 */
            for (var y = barY; y < barY + barHeight && y < _bitmapHeight; y++)
            {
                var rowOffset = y * w * 4;
                for (var x = barX; x < barX + barWidth && x < w; x++)
                {
                    var offset = rowOffset + x * 4;
                    ptr[offset] = 0x10;     // B
                    ptr[offset + 1] = 0x10; // G
                    ptr[offset + 2] = 0x10; // R
                    ptr[offset + 3] = 0x30; // A
                }
            }

            /* 填充部分 */
            var fillWidth = (int)(barWidth * Math.Clamp(_topProgressRatio, 0, 1));
            for (var y = barY; y < barY + barHeight && y < _bitmapHeight; y++)
            {
                var rowOffset = y * w * 4;
                for (var x = barX; x < barX + fillWidth && x < w; x++)
                {
                    var offset = rowOffset + x * 4;
                    ptr[offset] = b;
                    ptr[offset + 1] = g;
                    ptr[offset + 2] = r;
                    ptr[offset + 3] = alpha;
                }
            }
        }
    }

    /// <summary>将新像素与已有像素做 alpha 叠加混合（用于角落重叠区域）</summary>
    private static unsafe void BlendPixel(byte* ptr, int offset, byte r, byte g, byte b, byte a)
    {
        if (a == 0) return;

        var existingA = ptr[offset + 3];
        if (existingA == 0)
        {
            ptr[offset] = b;
            ptr[offset + 1] = g;
            ptr[offset + 2] = r;
            ptr[offset + 3] = a;
            return;
        }

        /* 简单叠加：取最大 alpha，颜色按 alpha 加权 */
        var outA = Math.Min(255, existingA + a);
        if (outA == 0) return;
        var srcRatio = (double)a / outA;
        var dstRatio = (double)existingA / outA;
        ptr[offset] = (byte)Math.Min(255, ptr[offset] * dstRatio + b * srcRatio);
        ptr[offset + 1] = (byte)Math.Min(255, ptr[offset + 1] * dstRatio + g * srcRatio);
        ptr[offset + 2] = (byte)Math.Min(255, ptr[offset + 2] * dstRatio + r * srcRatio);
        ptr[offset + 3] = (byte)outA;
    }

    private void ResolveVisual(
        out byte r, out byte g, out byte b,
        out double alphaFactor, out double pulsePeriod, out bool blink)
    {
        r = 0; g = 0; b = 0;
        alphaFactor = 0;
        pulsePeriod = 0;
        blink = false;

        switch (_state)
        {
            case EdgeOverlayState.Hidden:
                return;
            case EdgeOverlayState.Warning:
                r = 245; g = 138; b = 56;
                alphaFactor = 1;
                pulsePeriod = 3;
                break;
            case EdgeOverlayState.Hard:
                r = 230; g = 58; b = 58;
                alphaFactor = 1;
                pulsePeriod = 1;
                break;
            case EdgeOverlayState.RestBase:
                r = 67; g = 183; b = 108;
                alphaFactor = 1;
                pulsePeriod = 3;
                break;
            case EdgeOverlayState.RestElastic:
                r = 47; g = 206; b = 103;
                alphaFactor = 1.1;
                pulsePeriod = 1.8;
                break;
            case EdgeOverlayState.RestOvertime:
                r = 47; g = 206; b = 103;
                alphaFactor = 1.1;
                blink = true;
                pulsePeriod = 1;
                break;
            case EdgeOverlayState.Paused:
                r = 102; g = 102; b = 102;
                alphaFactor = 0.75;
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _animationTimer.Stop();
        _reTopmostTimer.Stop();
        if (_hBitmap != IntPtr.Zero)
        {
            Win32Native.DestroyGdiObject(_hBitmap);
            _hBitmap = IntPtr.Zero;
        }
        base.OnClosed(e);
    }
}
