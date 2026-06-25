using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ElasticBreath.App.Interop;

namespace ElasticBreath.App.UI;

/// <summary>
/// 边框覆盖状态枚举，用于表示各种覆盖状态。
/// </summary>
public enum EdgeOverlayState
{
    // 隐藏状态
    Hidden,
    // 警告状态
    Warning,
    // 硬状态（可能表示严格或固定状态）
    Hard,
    // 休息基础状态
    RestBase,
    // 休息弹性状态
    RestElastic,
    // 休息超时状态
    RestOvertime,
    // 暂停状态
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

    /// <summary>
    /// EdgeOverlayWindow 构造函数，初始化窗口组件和定时器，用于管理动画和窗口置顶。
    /// </summary>
    public EdgeOverlayWindow()
    {
// 调用InitializeComponent方法初始化UI组件
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
            // 检查窗口句柄是否有效且窗口可见时，强制置顶
            if (_hwnd != IntPtr.Zero && IsVisible) // 检查窗口句柄有效且窗口可见
                Win32Native.ForceTopmost(_hwnd); // 将窗口强制置顶
        };

                    // 订阅窗口初始化事件
        SourceInitialized += OnSourceInitialized;
    }

                /// <summary>
                /// 当窗口源初始化时调用。获取当前窗口的句柄，并将其设置为Layered Window，以禁用WPF的默认渲染。
                /// </summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // 获取当前窗口的本地句柄，用于后续Win32 API操作
        _hwnd = new WindowInteropHelper(this).Handle;
        /* 设置为 Layered Window，禁用 WPF 自身渲染 */
        // 调用Win32Native方法将窗口设置为Layered类型，实现自定义渲染
        Win32Native.SetLayered(_hwnd);
    }

    /// <summary>配置周期性重新置顶功能</summary>
    public void ConfigureReTopmost(bool enabled, int intervalSeconds)
    {
        intervalSeconds = Math.Max(1, intervalSeconds);
    /// <summary>
        /// 设置是否启用自动置顶功能及间隔时间。
        /// </summary>
        /// <param name="enabled">是否启用</param>
        /// <param name="intervalSeconds">间隔秒数</param>
        if (_reTopmostEnabled == enabled && _reTopmostIntervalSeconds == intervalSeconds)
            return; // 如果参数与当前值相同，则直接返回，避免重复设置
        _reTopmostEnabled = enabled; // 更新启用状态
        _reTopmostIntervalSeconds = intervalSeconds; // 更新间隔秒数
        _reTopmostTimer.Stop(); // 停止当前计时器，准备重新配置
/// <summary>
/// 根据启用状态和间隔秒数启动自动置顶定时器。
/// </summary>
        if (enabled) // 如果启用了自动置顶功能
        {
            _reTopmostTimer.Interval = TimeSpan.FromSeconds(intervalSeconds); // 设置计时器触发间隔
            _reTopmostTimer.Start(); // 启动计时器
        }
    }

    /// <summary>
    /// 设置控件在父容器中的边界区域（以像素为单位）。
    /// </summary>
    /// <param name="bounds">新的边界矩形，包含位置和尺寸信息。</param>
    public void SetBounds(System.Drawing.Rectangle bounds)
    {
        // 检查新的边界值是否与当前存储的值不同
// 检查传入的边界值是否与当前存储的边界值不同
        if (_boundsPx != bounds)
        {
            // 更新内部存储的边界值
            _boundsPx = bounds;
            // 标记位图（可能用于绘制）为“脏”，表示需要重新绘制
            _bitmapDirty = true;
        }
    }

/// <summary>
    /// 更新边缘覆盖层的显示状态和动画。
    /// </summary>
    /// <param name="state">边缘覆盖层的状态</param>
    /// <param name="enableEdgeGlow">是否启用边缘光晕效果</param>
    /// <param name="showTopProgress">是否显示顶部进度条</param>
    /// <param name="topProgressRatio">顶部进度条的比率值（0.0至1.0）</param>
    /// <param name="glowThickness">光晕效果的厚度（像素单位）</param>
    /// <param name="baseOpacity">基础不透明度（0.0至1.0）</param>
    /// <param name="hideAll">是否强制隐藏所有覆盖层元素</param>
    public void UpdateOverlay(
        EdgeOverlayState state,
        bool enableEdgeGlow,
        bool showTopProgress,
        double topProgressRatio,
        int glowThickness,
        double baseOpacity,
        bool hideAll)
    {
        // 将传入的参数保存到私有字段中
        _state = state;
        _enableEdgeGlow = enableEdgeGlow;
        _showTopProgress = showTopProgress;
        _glowThickness = glowThickness;
        _baseOpacity = baseOpacity;
        _topProgressRatio = topProgressRatio;
        // 记录动画开始的UTC时间，用于计算动画进度
        _animationStartUtc = DateTime.UtcNow;

        // 判断是否应该显示覆盖层窗口：当未全部隐藏且（启用光晕且状态可见 或 需要显示进度条）时才显示
        var shouldShowWindow = !hideAll && ((enableEdgeGlow && state != EdgeOverlayState.Hidden) || showTopProgress);
// 如果不满足显示窗口的条件
        if (!shouldShowWindow)
        {
            // 不需要显示时，隐藏窗口并停止动画定时器
            Hide();
            // 停止窗口动画计时器
            _animationTimer.Stop();
            // 提前返回，避免后续显示逻辑执行
            return;
        }

        /* 根据是否需要动画来启停定时器 */
        var needsAnimation = NeedsAnimation(state);
        // 如果需要动画但定时器未运行，则启动定时器
/// <summary>处理动画定时器和窗口显示逻辑</summary>
        // 如果需要动画且定时器未启动，则启动定时器
        if (needsAnimation && !_animationTimer.IsEnabled)
            _animationTimer.Start();
        // 如果不需要动画但定时器正在运行，则停止定时器
        else if (!needsAnimation && _animationTimer.IsEnabled)
            _animationTimer.Stop();

        // 如果窗口当前不可见，则显示它
        if (!IsVisible)
            Show();

        /* 立即渲染一帧 */
        // 调用渲染方法以更新视觉状态
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

    /// <summary>
    /// 渲染当前帧，处理动画效果并更新窗口显示。
    /// </summary>
    private void RenderFrame()
    {
        // 如果窗口句柄无效或不可见，直接返回
/// <summary>
/// 检查窗口句柄和可见性，解析视觉参数，并计算脉冲动画后的透明度因子。
/// </summary>
        // 如果窗口句柄无效或窗口不可见，则直接返回，不执行后续逻辑
        if (_hwnd == IntPtr.Zero || !IsVisible)
            return;

        // 解析视觉参数，如基础颜色、透明度因子、动画周期等
        ResolveVisual(out var baseR, out var baseG, out var baseB, out var alphaFactor, out var pulsePeriod, out var blink);

        /* 计算脉冲/闪烁后的最终权重 */
        var opacityFactor = alphaFactor;
        if (pulsePeriod > 0)
        {
            // 计算脉冲动画的相位和波形，基于正弦函数生成周期性变化
            var phase = (DateTime.UtcNow - _animationStartUtc).TotalSeconds / pulsePeriod;
            var wave = 0.5 + (0.5 * Math.Sin(phase * Math.PI * 2));
            // 将波形映射到0.35到1的范围内，用于调整透明度
            opacityFactor *= (0.35 + (0.65 * wave));
        }

/// <summary>
/// 处理闪烁动画效果的方法。
/// 根据当前时间计算闪烁状态，并动态调整不透明度因子。
/// </summary>
        if (blink)
        {
            // 计算当前动画时间已过去的总毫秒数
            var step = (int)((DateTime.UtcNow - _animationStartUtc).TotalMilliseconds / 500) % 2;
            // 根据计算出的步长值（0或1）决定最终的不透明度因子，步长为1时降低为0.2，实现闪烁效果
            opacityFactor *= step == 0 ? 1 : 0.2;
        }

        // 将基础透明度与动画因子合并，并限制在0到255范围内
        var finalAlpha = (byte)Math.Clamp(_baseOpacity * opacityFactor * 255, 0, 255);

        /* 如果颜色和 alpha 都没变，跳过渲染 */
// 检查是否需要更新渲染：如果位图未脏且颜色与上次相同，则直接返回
        if (!_bitmapDirty && finalAlpha == _lastA && baseR == _lastR && baseG == _lastG && baseB == _lastB)
            return;

        // 保存当前颜色和透明度状态，用于下次比较
        _lastA = finalAlpha;
        _lastR = baseR;
        _lastG = baseG;
        _lastB = baseB;

        /* 确保位图尺寸匹配 */
        EnsureBitmap();

        // 如果位图或像素指针无效，无法继续渲染，返回
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
// 检查宽高参数有效性，如果无效则直接返回
        if (w <= 0 || h <= 0)
            return;

        // 检查位图是否需要更新，如果未脏且尺寸未变则跳过
        if (!_bitmapDirty && _hBitmap != IntPtr.Zero && _bitmapWidth == w && _bitmapHeight == h)
            return;

        /* 释放旧位图 */
        // 释放旧位图资源，如果句柄有效
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
// 以下代码块负责初始化内存并准备绘制边缘光晕
            for (var i = 0; i < totalBytes; i++) // 遍历总字节数，初始化内存
                ptr[i] = 0; // 将指针数组的每个元素清零
        }

        // 检查透明度是否为0，若为0则跳过后续绘制
        if (alpha == 0) // 如果透明度值为0
            return; // 则提前返回，跳过后续绘制逻辑

        // 缓存光晕厚度值到局部变量以提高性能
        var t = _glowThickness; // 将光晕厚度值存储到局部变量t中

        // 开始绘制边缘光晕的条件判断
        /* 绘制四条边缘的渐变光晕 */ // 块注释：说明接下来的代码负责绘制边缘光晕
        if (_enableEdgeGlow) // 检查边缘光晕是否启用
        {
            unsafe
            {
                var ptr = (byte*)_pixels;

                /* 上边缘：从上到下渐变，alpha 从 alpha 渐变到 0 */
                // 遍历从0到min(t, h)的行，实现渐变填充效果
                /// <summary>
                /// 在图像数据中绘制一个从上到下透明度渐变的矩形区域。
                /// 外循环遍历行（控制垂直方向），内循环遍历列（水平方向），为每个像素设置BGRA颜色值以实现渐变效果。
                /// </summary>
                for (var y = 0; y < t && y < h; y++)
                {
                    var ratio = 1.0 - (double)y / t; // 计算当前行与总行数t的比例因子，用于渐变计算
                    var a = (byte)(alpha * ratio);   // 根据比例因子调整alpha值，实现透明度渐变
                    var rowOffset = y * stride;      // 计算当前行在图像数据中的字节偏移量
                    for (var x = 0; x < w; x++)       // 遍历当前行的每个像素
                    {
                        var offset = rowOffset + x * 4; // 计算当前像素在图像数据中的起始偏移量，每个像素占4字节（BGRA格式）
                        ptr[offset] = b;       // 设置像素的蓝色分量
                        ptr[offset + 1] = g;   // 设置像素的绿色分量
                        ptr[offset + 2] = r;   // 设置像素的红色分量
                        ptr[offset + 3] = a;   // 设置像素的alpha透明度分量
                    }
                }

                /* 下边缘：从下到上渐变 */
                /// <summary>
                /// 在图像上应用一个渐变效果，设置指定区域的颜色和透明度。
                /// </summary>
                for (var y = Math.Max(0, h - t); y < h; y++)
                {
                    // 计算当前y坐标在渐变区域内的比例，用于alpha渐变
                    var ratio = (double)(y - (h - t)) / t;
                    // 根据比例计算alpha值，实现透明度渐变
                    var a = (byte)(alpha * ratio);
                    // 计算当前行的字节偏移量
                    var rowOffset = y * stride;
                    for (var x = 0; x < w; x++)
                    {
                        // 计算当前像素的字节偏移量（假设每个像素4字节：B, G, R, A）
                        var offset = rowOffset + x * 4;
                        // 设置蓝色通道
                        ptr[offset] = b;
                        // 设置绿色通道
                        ptr[offset + 1] = g;
                        // 设置红色通道
                        ptr[offset + 2] = r;
                        // 设置alpha通道
                        ptr[offset + 3] = a;
                    }
                }

                /* 左边缘：从左到右渐变 */
/// <summary>
/// 执行像素混合操作，遍历图像高度和宽度，根据x坐标计算线性变化的alpha比率，并调用BlendPixel进行alpha混合。
/// </summary>
                for (var y = 0; y < h; y++)  // 遍历每一行像素
                {
                    var rowOffset = y * stride;  // 计算当前行在内存中的起始偏移量
                    for (var x = 0; x < t && x < w; x++)  // 遍历每列，确保x不超过t和w的最小值
                    {
                        var ratio = 1.0 - (double)x / t;  // 计算alpha比率，从1.0线性减少到0.0，实现渐变效果
                        var a = (byte)(alpha * ratio);  // 将基础alpha值乘以比率，并转换为字节类型
                        var offset = rowOffset + x * 4;  // 计算当前像素的字节偏移（假设每个像素占4字节，如RGBA格式）
                        /* 与已有像素做 alpha 混合（叠加） */
                        BlendPixel(ptr, offset, r, g, b, a);  // 调用BlendPixel函数，将当前颜色与目标像素进行alpha混合
                    }
                }

                /* 右边缘：从右到左渐变 */
/// <summary>
/// 此方法用于执行像素混合操作，实现图像边缘的渐变效果。
/// </summary>
                for (var y = 0; y < h; y++)  // 遍历图像的每一行
                {
                    var rowOffset = y * stride;  // 计算当前行的起始偏移量
/// <summary>
/// 这个for循环实现从右侧开始的像素遍历，用于生成透明度渐变效果。
/// </summary>
                    for (var x = Math.Max(0, w - t); x < w; x++)  // 从右侧开始遍历像素，t 为渐变区域的宽度
                    {
                        var ratio = (double)(x - (w - t)) / t;  // 计算当前像素在渐变区域中的位置比率
                        var a = (byte)(alpha * ratio);  // 根据比率和基础 alpha 值计算当前像素的透明度
                        var offset = rowOffset + x * 4;  // 计算当前像素在内存中的偏移量（假设每像素占4字节）
                        BlendPixel(ptr, offset, r, g, b, a);  // 将计算出的颜色和透明度混合到指定像素
                    }
                }
            }
        }

        /* 绘制顶部进度条 */
// 如果显示顶部进度条的条件为真
        if (_showTopProgress)
        {
            // 调用绘制进度条的方法，传入颜色和透明度参数
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
// 绘制矩形条的循环：在位图上填充指定区域的像素颜色
/// <summary>
/// 绘制一个半透明的矩形区域到位图中。
/// 此方法通过遍历指定矩形区域内的像素，将每个像素的颜色设置为深灰色（BGR: 0x10, 0x10, 0x10）
/// 并设置透明度为0x30，从而实现半透明的视觉效果。
/// </summary>
            for (var y = barY; y < barY + barHeight && y < _bitmapHeight; y++) // 遍历y坐标，从起始位置到结束位置，确保不超出位图高度
            {
                var rowOffset = y * w * 4; // 计算当前行在像素数据中的字节偏移量（假设每个像素4字节，格式为BGRA）
                for (var x = barX; x < barX + barWidth && x < w; x++) // 遍历x坐标，从起始位置到结束位置，确保不超出位图宽度
                {
                    var offset = rowOffset + x * 4; // 计算当前像素在数据中的具体字节偏移量
                    ptr[offset] = 0x10;     // 设置蓝色通道值（B）为0x10
                    ptr[offset + 1] = 0x10; // 设置绿色通道值（G）为0x10
                    ptr[offset + 2] = 0x10; // 设置红色通道值（R）为0x10
                    ptr[offset + 3] = 0x30; // 设置透明度通道值（A）为0x30，实现半透明效果
                }
            }

            /* 填充部分 */
            var fillWidth = (int)(barWidth * Math.Clamp(_topProgressRatio, 0, 1));
            /// <summary>
            /// 在位图中绘制一个指定颜色和透明度的条形区域，通过指针直接操作像素数据。
            /// </summary>
            for (var y = barY; y < barY + barHeight && y < _bitmapHeight; y++) // 外层循环：遍历y坐标，从barY开始，直到barY + barHeight或位图高度，以较小者为准
            {
                var rowOffset = y * w * 4; // 计算当前行的字节偏移量，每像素4字节（BGRA格式）
                /// <summary>
                /// 内层循环，用于遍历x坐标并设置像素颜色分量。
                /// </summary>
                for (var x = barX; x < barX + fillWidth && x < w; x++) // 内层循环：遍历x坐标，从barX开始，直到barX + fillWidth或位图宽度，以较小者为准
                {
                    var offset = rowOffset + x * 4; // 计算当前像素的绝对字节偏移量
                    ptr[offset] = b; // 设置像素的蓝色分量
                    ptr[offset + 1] = g; // 设置像素的绿色分量
                    ptr[offset + 2] = r; // 设置像素的红色分量
                    ptr[offset + 3] = alpha; // 设置像素的透明度分量
                }
            }
        }
    }

    /// <summary>将新像素与已有像素做 alpha 叠加混合（用于角落重叠区域）</summary>
    private static unsafe void BlendPixel(byte* ptr, int offset, byte r, byte g, byte b, byte a)
    {
// 如果 a 为 0，则直接返回，避免后续处理
        if (a == 0) return;

        // 从 ptr 数组的 offset + 3 位置获取现有 alpha 值
        var existingA = ptr[offset + 3];
        // 如果现有 alpha 值为 0，表示该像素点未设置过颜色
        if (existingA == 0)
        {
            // 按照 BGRA 顺序设置像素颜色值
            ptr[offset] = b;
            ptr[offset + 1] = g;
            ptr[offset + 2] = r;
            ptr[offset + 3] = a;
            // 设置完成后返回
            return;
        }

        /* 简单叠加：取最大 alpha，颜色按 alpha 加权 */
        var outA = Math.Min(255, existingA + a);
// 如果输出alpha值为0，则直接返回，避免除以零错误
        if (outA == 0) return;
        // 计算源颜色的alpha比率，用于混合计算
        var srcRatio = (double)a / outA;
        // 计算目标颜色的alpha比率，用于混合计算
        var dstRatio = (double)existingA / outA;
        // 更新蓝色通道值：将目标蓝色与源蓝色混合，并确保结果不超过255
        ptr[offset] = (byte)Math.Min(255, ptr[offset] * dstRatio + b * srcRatio);
        // 更新绿色通道值：类似地混合绿色通道
        ptr[offset + 1] = (byte)Math.Min(255, ptr[offset + 1] * dstRatio + g * srcRatio);
        // 更新红色通道值：类似地混合红色通道
        ptr[offset + 2] = (byte)Math.Min(255, ptr[offset + 2] * dstRatio + r * srcRatio);
        // 设置最终的alpha通道值为输出alpha
        ptr[offset + 3] = (byte)outA;
    }

    /// <summary>
    /// 解析视觉效果参数，用于确定颜色、透明度、脉冲和闪烁状态。
    /// </summary>
    /// <param name="r">输出的红色颜色分量（字节值）</param>
    /// <param name="g">输出的绿色颜色分量（字节值）</param>
    /// <param name="b">输出的蓝色颜色分量（字节值）</param>
    /// <param name="alphaFactor">输出的透明度因子（双精度浮点数）</param>
    /// <param name="pulsePeriod">输出的脉冲周期（双精度浮点数，单位可能为秒）</param>
    /// <param name="blink">输出的闪烁状态（布尔值，true表示闪烁）</param>
    private void ResolveVisual(
        out byte r, out byte g, out byte b,
        out double alphaFactor, out double pulsePeriod, out bool blink)
    {
        r = 0; g = 0; b = 0;
        alphaFactor = 0;
        pulsePeriod = 0;
        blink = false;

        /// <summary>
        /// 根据当前边缘覆盖状态（EdgeOverlayState），设置对应的颜色（RGB）、透明度因子、闪烁和脉冲周期等视觉参数。
        /// </summary>
        switch (_state)
        {
            case EdgeOverlayState.Hidden:
                // 隐藏状态：无需任何绘制，直接返回
                return;
            case EdgeOverlayState.Warning:
                // 警告状态：橙色（245,138,56），完全不透明，脉冲周期3秒
                r = 245; g = 138; b = 56;
                alphaFactor = 1;
                pulsePeriod = 3;
                break;
            case EdgeOverlayState.Hard:
                // 强制/硬状态：红色（230,58,58），完全不透明，快速脉冲（1秒）
                r = 230; g = 58; b = 58;
                alphaFactor = 1;
                pulsePeriod = 1;
                break;
            case EdgeOverlayState.RestBase:
                // 休息基础状态：绿色（67,183,108），完全不透明，脉冲周期3秒
                r = 67; g = 183; b = 108;
                alphaFactor = 1;
                pulsePeriod = 3;
                break;
            case EdgeOverlayState.RestElastic:
                // 休息弹性状态：稍亮的绿色（47,206,103），不透明度略高于1（1.1），脉冲周期1.8秒
                r = 47; g = 206; b = 103;
                alphaFactor = 1.1;
                pulsePeriod = 1.8;
                break;
            case EdgeOverlayState.RestOvertime:
                // 休息超时状态：与弹性休息相同的绿色和透明度，但启用闪烁效果，脉冲周期加快至1秒
                r = 47; g = 206; b = 103;
                alphaFactor = 1.1;
                blink = true;
                pulsePeriod = 1;
                break;
            case EdgeOverlayState.Paused:
                // 暂停状态：灰色（102,102,102），半透明（透明度因子0.75）
                r = 102; g = 102; b = 102;
                alphaFactor = 0.75;
                break;
        }
    }

    /// <summary>
    /// 窗口关闭时执行的清理方法，用于释放相关资源。
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        // 停止动画计时器
        _animationTimer.Stop();
        // 停止置顶计时器
        _reTopmostTimer.Stop();
        // 检查并释放GDI位图资源，防止内存泄漏
/// <summary>
/// 安全销毁位图句柄：检查句柄是否有效，如果是则销毁GDI对象并置零。
/// </summary>
        if (_hBitmap != IntPtr.Zero)  // 检查位图句柄是否非零
        {
            Win32Native.DestroyGdiObject(_hBitmap);  // 调用Win32API销毁GDI对象
            // 将句柄置零，避免重复释放
            _hBitmap = IntPtr.Zero;
        }
        // 调用基类的OnClosed方法完成基础关闭流程
        base.OnClosed(e);
    }
}
