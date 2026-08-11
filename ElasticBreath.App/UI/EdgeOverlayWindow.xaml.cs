using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ElasticBreath.App.Interop;
using ElasticBreath.Rendering;

namespace ElasticBreath.App.UI;

/// <summary>
/// 边缘光晕覆盖层，使用 Win32 UpdateLayeredWindow 直接渲染像素。
/// 完全绕过 WPF 渲染管线，GPU 占用极低。
/// 像素填充逻辑（颜色表、四边渐变、顶部进度条）委托给 <see cref="EdgeOverlayPixelRenderer"/>，
/// 与离线截图工具共用同一份渲染代码，保证实机与截图像素级一致。
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

        /* 动画定时器，50ms 间隔（~20FPS）。
         * 原 200ms（5FPS）对 1~3 秒周期的正弦脉冲渐变阶梯感明显；
         * 配合像素清空改用 native memset，20FPS 的总 CPU 占用反而低于原 5FPS。 */
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
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
        var visual = EdgeOverlayPixelRenderer.ResolveVisual(_state);

        /* 计算脉冲/闪烁后的最终权重 */
        var opacityFactor = EdgeOverlayPixelRenderer.ComputeOpacityFactor(
            visual.AlphaFactor, visual.PulsePeriod, visual.Blink,
            DateTime.UtcNow - _animationStartUtc);

        // 将基础透明度与动画因子合并，并限制在0到255范围内
        var finalAlpha = (byte)Math.Clamp(_baseOpacity * opacityFactor * 255, 0, 255);

        /* 如果颜色和 alpha 都没变，跳过渲染 */
// 检查是否需要更新渲染：如果位图未脏且颜色与上次相同，则直接返回
        if (!_bitmapDirty && finalAlpha == _lastA && visual.R == _lastR && visual.G == _lastG && visual.B == _lastB)
        {
            return;
        }

        // 保存当前颜色和透明度状态，用于下次比较
        _lastA = finalAlpha;
        _lastR = visual.R;
        _lastG = visual.G;
        _lastB = visual.B;

        /* 确保位图尺寸匹配 */
        EnsureBitmap();

        // 如果位图或像素指针无效，无法继续渲染，返回
        if (_hBitmap == IntPtr.Zero || _pixels == IntPtr.Zero)
            return;

        /* 定位窗口 */
        Win32Native.SetWindowBoundsPixels(_hwnd, _boundsPx);

        /* 填充像素 */
        unsafe
        {
            EdgeOverlayPixelRenderer.Render(
                (byte*)_pixels, _bitmapWidth, _bitmapHeight,
                visual.R, visual.G, visual.B, finalAlpha,
                _glowThickness, _enableEdgeGlow, _showTopProgress, _topProgressRatio);
        }

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
