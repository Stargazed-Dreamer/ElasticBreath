using System.Windows;
using System.Windows.Media.Animation;

namespace ElasticBreath.App.UI;

/// <summary>
/// 一个用于显示简短提示消息的Toast窗口类，支持动画效果。
/// 该窗口会根据目标矩形的位置进行定位，并以动画形式显示和隐藏。
/// </summary>
public partial class ToastWindow : Window
{
    // 进入动画的持续时间，约为280毫秒。
    private readonly Duration _inDuration = new(TimeSpan.FromMilliseconds(280));
    // 退出动画的持续时间，约为220毫秒。
    private readonly Duration _outDuration = new(TimeSpan.FromMilliseconds(220));

/// <summary>
    /// 初始化ToastWindow类的新实例。
    /// </summary>
    public ToastWindow()
    {
        // 初始化窗口组件。
        InitializeComponent();
    }

    /// <summary>
    /// 在指定目标矩形附近显示一个Toast消息。
    /// </summary>
    /// <param name="message">要显示的提示文本。</param>
    /// <param name="targetBounds">用于定位Toast的目标区域矩形。</param>
    public void ShowMessage(string message, Rect targetBounds)
    {
        // 设置提示文本内容。
        ToastText.Text = message;

        // 计算Toast窗口的最终左侧位置（水平居中对齐于目标区域右侧，并留出边距）。
        var finalLeft = targetBounds.Right - Width - 24;
        // 计算Toast窗口的顶部位置（紧贴目标区域底部上方，并留出边距）。
        var top = targetBounds.Bottom - Height - 24;
        // 先将窗口移动到进入动画的起始位置（在最终位置右侧44像素处）。
        Left = finalLeft + 44;
        Top = top;

        // 如果窗口当前不可见，则先显示窗口。
// 如果IsVisible属性为假，则执行Show方法
        if (!IsVisible)
        {
            Show(); // 调用Show方法进行显示
        }

        // 创建一个用于进入的双精度动画，将Left属性从当前位置动画到最终计算的位置。
        var inAnim = new DoubleAnimation
        {
            To = finalLeft,
            Duration = _inDuration,
            // 使用缓出（EaseOut）的立方缓动函数，使动画开始快、结束慢。
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        // 开始播放进入动画。
/// <summary>
/// 启动指定属性的动画，并设置定时器在保持时间后触发动画结束事件。
/// </summary>
        BeginAnimation(LeftProperty, inAnim);

        // 定义Toast消息的显示保持时间。
        var hold = TimeSpan.FromMilliseconds(1100);
        // 创建一个调度器定时器，用于在保持时间结束后启动退出动画。
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = hold };
        // 定时器触发事件（即保持时间结束）。
        timer.Tick += (_, _) =>
        {
            // 停止定时器。
            timer.Stop();
            // 创建一个用于退出的双精度动画，将Left属性从当前位置动画到更右侧的位置（模拟滑出）。
            var outAnim = new DoubleAnimation
            {
                To = finalLeft + 52,
                Duration = _outDuration,
                // 使用缓入（EaseIn）的立方缓动函数，使动画开始慢、结束快。
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            // 当退出动画完成时，隐藏整个Toast窗口。
            outAnim.Completed += (_, _) => Hide();
            // 开始播放退出动画。
            BeginAnimation(LeftProperty, outAnim);
        };
        // 启动定时器，开始计算保持时间。
        timer.Start();
    }
}
