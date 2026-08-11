using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ElasticBreath.App.Interop;
using MediaColor = System.Windows.Media.Color;

namespace ElasticBreath.App.UI;

/// <summary>
/// 左上角角落触发的视觉指示器：一个圆心位于屏幕左上角的半透明实心圆。
/// 鼠标进入左上角时弹性胀大到半径 20px（出现时快速胀大略超 20px 再回弹），
/// 悬停期间颜色由灰渐变到主题绿；切换完成后保持绿色，
/// 鼠标移出左上角后弹性收回。窗口置顶、点击穿透、不抢焦点。
/// 动画由 DispatcherTimer 手动逐帧驱动（与 EdgeOverlayWindow 同一模式）——
/// 避免 Storyboard 在窗口首次显示、视觉树未就绪时静默失败导致圆不可见。
/// </summary>
public partial class CornerIndicatorWindow : Window
{
    /// <summary>初始灰色：#7F7F7F，不透明度 55%</summary>
    private static readonly MediaColor Gray = MediaColor.FromArgb((byte)(255 * 0.55), 0x7F, 0x7F, 0x7F);
    /// <summary>主题绿：#32B265，不透明度 80%</summary>
    private static readonly MediaColor ThemeGreen = MediaColor.FromArgb((byte)(255 * 0.80), 0x32, 0xB2, 0x65);

    /// <summary>圆的最大半径（像素）。圆直径在 XAML 中固定为 40（半径 20）。</summary>
    private const double OvershootScale = 1.2;

    // 出现动画：0 → 1.2（过冲）→ 1.0，总时长 240ms
    private const double AppearOvershootMs = 90;
    private const double AppearSettleMs = 150;
    // 收回动画：1.0 → 0.9 → 1.05 → 0，总时长 200ms
    private const double RetractBounce1Ms = 30;
    private const double RetractBounce2Ms = 30;
    private const double RetractShrinkMs = 140;

    private readonly DispatcherTimer _animTimer;
    private DateTime _animStartUtc;
    private bool _appearing;
    private bool _retracting;
    private bool _visible;

    public CornerIndicatorWindow()
    {
        InitializeComponent();
        // 点击穿透 + 不激活 + 不占任务栏，鼠标事件全部穿透到下层窗口
        SourceInitialized += (_, _) =>
            Win32Native.SetClickThroughNoActivate(new WindowInteropHelper(this).Handle);

        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animTimer.Tick += OnAnimTick;
    }

    /// <summary>
    /// 在屏幕左上角显示圆并更新悬停进度。
    /// 首次显示时播放弹性胀大动画；后续调用仅更新颜色（灰→绿渐变）。
    /// </summary>
    /// <param name="leftPx">目标屏幕左上角 X（像素）</param>
    /// <param name="topPx">目标屏幕左上角 Y（像素）</param>
    /// <param name="progress">悬停进度 0.0~1.0</param>
    public void ShowAt(int leftPx, int topPx, double progress)
    {
        if (!_visible)
        {
            _visible = true;
            Show();
            // 像素坐标 → WPF 设备无关单位（适配不同 DPI）
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = leftPx / dpi.DpiScaleX;
            Top = topPx / dpi.DpiScaleY;
            StartAppearAnimation();
        }

        Circle.Fill = new SolidColorBrush(Lerp(Gray, ThemeGreen, progress));
    }

    /// <summary>弹性收回并隐藏圆。鼠标移出左上角区域时调用。</summary>
    public void Retract()
    {
        if (!_visible)
        {
            return;
        }
        _visible = false;
        StartRetractAnimation();
    }

    private void StartAppearAnimation()
    {
        _appearing = true;
        _retracting = false;
        Circle.Opacity = 0;
        SetScale(0);
        _animStartUtc = DateTime.UtcNow;
        if (!_animTimer.IsEnabled)
        {
            _animTimer.Start();
        }
    }

    private void StartRetractAnimation()
    {
        _retracting = true;
        _appearing = false;
        _animStartUtc = DateTime.UtcNow;
        if (!_animTimer.IsEnabled)
        {
            _animTimer.Start();
        }
    }

    /// <summary>逐帧驱动弹性动画：先弹性胀大/轻微回弹，再淡入淡出。</summary>
    private void OnAnimTick(object? sender, EventArgs e)
    {
        var ms = (DateTime.UtcNow - _animStartUtc).TotalMilliseconds;

        if (_appearing)
        {
            double scale, opacity;
            if (ms < AppearOvershootMs)
            {
                var t = ms / AppearOvershootMs;
                scale = OvershootScale * EaseOutCubic(t); // 0 → 1.2
                opacity = Math.Min(1, t * 1.5);
            }
            else if (ms < AppearOvershootMs + AppearSettleMs)
            {
                var t = (ms - AppearOvershootMs) / AppearSettleMs;
                scale = OvershootScale + (1 - OvershootScale) * EaseOutCubic(t); // 1.2 → 1.0
                opacity = 1;
            }
            else
            {
                scale = 1;
                opacity = 1;
                _animTimer.Stop();
            }
            SetScale(scale);
            Circle.Opacity = opacity;
        }
        else if (_retracting)
        {
            double scale, opacity;
            if (ms < RetractBounce1Ms)
            {
                scale = 1 + (0.9 - 1) * (ms / RetractBounce1Ms); // 1.0 → 0.9
                opacity = 1;
            }
            else if (ms < RetractBounce1Ms + RetractBounce2Ms)
            {
                scale = 0.9 + (1.05 - 0.9) * ((ms - RetractBounce1Ms) / RetractBounce2Ms); // 0.9 → 1.05
                opacity = 1;
            }
            else if (ms < RetractBounce1Ms + RetractBounce2Ms + RetractShrinkMs)
            {
                var t = (ms - RetractBounce1Ms - RetractBounce2Ms) / RetractShrinkMs;
                scale = 1.05 * (1 - t); // 1.05 → 0
                opacity = 1 - t;
            }
            else
            {
                scale = 0;
                opacity = 0;
                _animTimer.Stop();
                Hide();
            }
            SetScale(scale);
            Circle.Opacity = opacity;
        }
    }

    private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);

    private void SetScale(double s)
    {
        CircleScale.ScaleX = s;
        CircleScale.ScaleY = s;
    }

    /// <summary>在两个颜色（含 alpha）间线性插值。</summary>
    private static MediaColor Lerp(MediaColor from, MediaColor to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return MediaColor.FromArgb(
            (byte)(from.A + (to.A - from.A) * t),
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t));
    }
}
