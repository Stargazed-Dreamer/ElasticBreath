using System.Windows;
using System.Windows.Media.Animation;

namespace ElasticBreath.App.UI;

public partial class ToastWindow : Window
{
    private readonly Duration _inDuration = new(TimeSpan.FromMilliseconds(280));
    private readonly Duration _outDuration = new(TimeSpan.FromMilliseconds(220));

    public ToastWindow()
    {
        InitializeComponent();
    }

    public void ShowMessage(string message, Rect targetBounds)
    {
        ToastText.Text = message;

        var finalLeft = targetBounds.Right - Width - 24;
        var top = targetBounds.Bottom - Height - 24;
        Left = finalLeft + 44;
        Top = top;

        if (!IsVisible)
        {
            Show();
        }

        var inAnim = new DoubleAnimation
        {
            To = finalLeft,
            Duration = _inDuration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(LeftProperty, inAnim);

        var hold = TimeSpan.FromMilliseconds(1100);
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = hold };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var outAnim = new DoubleAnimation
            {
                To = finalLeft + 52,
                Duration = _outDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            outAnim.Completed += (_, _) => Hide();
            BeginAnimation(LeftProperty, outAnim);
        };
        timer.Start();
    }
}
