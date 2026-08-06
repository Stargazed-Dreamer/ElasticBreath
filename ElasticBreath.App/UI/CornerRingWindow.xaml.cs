using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ElasticBreath.App.Interop;
using ElasticBreath.App.Services;

namespace ElasticBreath.App.UI;

/// <summary>
/// 角落悬停倒计时圆环窗口。
/// 鼠标停留在屏幕角落期间，在对应角落浮现一个极简圆环，随悬停时长从 0 填满到 1，
/// 填满瞬间由 <see cref="CornerTriggerService"/> 触发状态切换，圆环随即隐藏。
/// 设计参考：design.md §5.3（"浮现极简倒计时圆环，填满瞬间静默切入下一状态"）。
/// </summary>
public partial class CornerRingWindow : Window
{
    private const int WindowSize = 44;
    private const int InsetPixels = 10; // 距屏幕边缘内缩，避免被裁切
    private const double RingRadius = 15.0;
    private static readonly System.Windows.Point RingCenter = new(15, 15);

    private IntPtr _handle;

    public CornerRingWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            Win32Native.SetClickThroughNoActivate(_handle);
        };
    }

    /// <summary>
    /// 根据当前悬停进度更新圆环：定位到角落、刷新弧形、控制显隐。
    /// </summary>
    /// <param name="hover">悬停状态（角落标识 + 进度）</param>
    /// <param name="screenBounds">目标屏幕物理像素边界</param>
    public void Update(CornerHoverState hover, Rectangle screenBounds)
    {
        if (hover.Corner is null)
        {
            if (IsVisible)
            {
                Hide();
            }
            return;
        }

        PositionAtCorner(hover.Corner, screenBounds);
        ProgressArc.Data = BuildArcGeometry(RingCenter, RingRadius, hover.Progress);

        if (!IsVisible)
        {
            Show();
        }
    }

    /// <summary>按角落标识把窗口定位到屏幕对应角落（物理像素坐标系，规避 DPI 偏移）。</summary>
    private void PositionAtCorner(string corner, Rectangle b)
    {
        int x, y;
        switch (corner)
        {
            case "LT":
                x = b.Left + InsetPixels;
                y = b.Top + InsetPixels;
                break;
            case "RT":
                x = b.Right - WindowSize - InsetPixels;
                y = b.Top + InsetPixels;
                break;
            case "LB":
                x = b.Left + InsetPixels;
                y = b.Bottom - WindowSize - InsetPixels;
                break;
            case "RB":
                x = b.Right - WindowSize - InsetPixels;
                y = b.Bottom - WindowSize - InsetPixels;
                break;
            default:
                return;
        }

        if (_handle != IntPtr.Zero)
        {
            // 用 Win32 物理像素定位，避免高 DPI 下 WPF Left/Top 的逻辑像素偏移
            Win32Native.SetWindowBoundsPixels(_handle, new Rectangle(x, y, WindowSize, WindowSize));
        }
        else
        {
            // 句柄尚未就绪（极早期），回退到 WPF 单位
            Left = x;
            Top = y;
        }
    }

    /// <summary>构建圆弧几何：起始角 -90°（12 点方向），顺时针填充 progress 比例。</summary>
    private static Geometry BuildArcGeometry(System.Windows.Point center, double radius, double progressRatio)
    {
        progressRatio = Math.Clamp(progressRatio, 0, 1);
        if (progressRatio <= 0)
        {
            return Geometry.Empty;
        }
        if (progressRatio >= 0.9999)
        {
            return new EllipseGeometry(center, radius, radius);
        }

        var startAngle = -90d;
        var endAngle = startAngle + (360d * progressRatio);
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, endAngle);
        var largeArc = progressRatio > 0.5;

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new System.Windows.Size(radius, radius), 0, largeArc, SweepDirection.Clockwise, true));
        return new PathGeometry(new[] { figure });
    }

    private static System.Windows.Point PointOnCircle(System.Windows.Point center, double radius, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180d;
        return new System.Windows.Point(center.X + (radius * Math.Cos(rad)), center.Y + (radius * Math.Sin(rad)));
    }
}
