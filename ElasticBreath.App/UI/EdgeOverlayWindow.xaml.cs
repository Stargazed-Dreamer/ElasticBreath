using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ElasticBreath.App.Interop;
using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;

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

public partial class EdgeOverlayWindow : Window
{
    private readonly DispatcherTimer _animationTimer;
    private DateTime _animationStartUtc = DateTime.UtcNow;
    private MediaColor _baseColor = MediaColors.Transparent;
    private double _baseOpacity = 0.3;
    private int _glowThickness = 80;
    private EdgeOverlayState _state = EdgeOverlayState.Hidden;
    private bool _enableEdgeGlow = true;
    private bool _showTopProgress;

    public EdgeOverlayWindow()
    {
        InitializeComponent();

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(66)
        };
        _animationTimer.Tick += (_, _) => RenderFrame();
        _animationTimer.Start();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32Native.SetClickThroughNoActivate(hwnd);
        };
    }

    public void SetBounds(Rect bounds)
    {
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
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
        _animationStartUtc = DateTime.UtcNow;

        topProgressRatio = Math.Clamp(topProgressRatio, 0, 1);
        TopProgressFill.Width = TopProgressHost.Width * topProgressRatio;

        var shouldShowWindow = !hideAll && ((enableEdgeGlow && state != EdgeOverlayState.Hidden) || showTopProgress);
        if (!shouldShowWindow)
        {
            Hide();
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        TopProgressHost.Visibility = showTopProgress ? Visibility.Visible : Visibility.Collapsed;
        RenderFrame();
    }

    private void RenderFrame()
    {
        if (!IsVisible)
        {
            return;
        }

        ResolveVisual(out var baseColor, out var alphaFactor, out var pulsePeriod, out var blink);
        _baseColor = baseColor;

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

        var finalOpacity = Math.Clamp(_baseOpacity * opacityFactor, 0, 1);
        var glowColor = MediaColor.FromArgb((byte)(finalOpacity * 255), _baseColor.R, _baseColor.G, _baseColor.B);
        var transparent = MediaColor.FromArgb(0, _baseColor.R, _baseColor.G, _baseColor.B);

        if (_enableEdgeGlow)
        {
            TopGlow.Height = _glowThickness;
            BottomGlow.Height = _glowThickness;
            LeftGlow.Width = _glowThickness;
            RightGlow.Width = _glowThickness;

            TopGlow.Fill = new LinearGradientBrush(glowColor, transparent, new Point(0.5, 0), new Point(0.5, 1));
            BottomGlow.Fill = new LinearGradientBrush(glowColor, transparent, new Point(0.5, 1), new Point(0.5, 0));
            LeftGlow.Fill = new LinearGradientBrush(glowColor, transparent, new Point(0, 0.5), new Point(1, 0.5));
            RightGlow.Fill = new LinearGradientBrush(glowColor, transparent, new Point(1, 0.5), new Point(0, 0.5));
        }
        else
        {
            TopGlow.Fill = Brushes.Transparent;
            BottomGlow.Fill = Brushes.Transparent;
            LeftGlow.Fill = Brushes.Transparent;
            RightGlow.Fill = Brushes.Transparent;
        }

        if (_showTopProgress)
        {
            TopProgressFill.Background = new SolidColorBrush(glowColor);
        }
    }

    private void ResolveVisual(out MediaColor color, out double alphaFactor, out double pulsePeriod, out bool blink)
    {
        color = MediaColors.Transparent;
        alphaFactor = 0;
        pulsePeriod = 0;
        blink = false;

        switch (_state)
        {
            case EdgeOverlayState.Hidden:
                return;
            case EdgeOverlayState.Warning:
                color = MediaColor.FromRgb(245, 138, 56);
                alphaFactor = 1;
                pulsePeriod = 3;
                break;
            case EdgeOverlayState.Hard:
                color = MediaColor.FromRgb(230, 58, 58);
                alphaFactor = 1;
                pulsePeriod = 1;
                break;
            case EdgeOverlayState.RestBase:
                color = MediaColor.FromRgb(67, 183, 108);
                alphaFactor = 1;
                pulsePeriod = 3;
                break;
            case EdgeOverlayState.RestElastic:
                color = MediaColor.FromRgb(47, 206, 103);
                alphaFactor = 1.1;
                pulsePeriod = 1.8;
                break;
            case EdgeOverlayState.RestOvertime:
                color = MediaColor.FromRgb(47, 206, 103);
                alphaFactor = 1.1;
                blink = true;
                pulsePeriod = 1;
                break;
            case EdgeOverlayState.Paused:
                color = MediaColor.FromRgb(102, 102, 102);
                alphaFactor = 0.75;
                break;
        }
    }
}
