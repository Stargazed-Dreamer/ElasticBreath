using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ElasticBreath.App.Interop;
using Drawing = System.Drawing;

namespace ElasticBreath.App.UI;

/// <summary>
/// 倒计时通知窗口类，用于显示倒计时通知并处理用户交互。
/// </summary>
public partial class CountdownNotificationWindow : Window
{
    // 窗口句柄，用于原生窗口操作
    private IntPtr _hwnd;
    // 窗口在像素单位下的边界，默认大小为420x84
    private Drawing.Rectangle _boundsPx = new(0, 0, 420, 84);

/// <summary>
/// 初始化CountdownNotificationWindow窗口，设置事件处理程序，并在窗口源初始化时获取句柄和设置窗口边界。
/// </summary>
    public CountdownNotificationWindow()
    {
        InitializeComponent(); // 初始化WPF组件
        MouseDown += OnMouseDown; // 绑定鼠标按下事件处理程序
        // 在窗口源初始化时获取句柄并设置窗口边界
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle; // 获取当前窗口的句柄
            Win32Native.SetWindowBoundsPixels(_hwnd, _boundsPx); // 使用原生方法设置窗口像素边界
        };
    }

    // 取消请求事件，当用户点击窗口时触发
    public event EventHandler? CancelRequested;

    /// <summary>
    /// 将窗口定位到目标边界区域的右上角。
    /// </summary>
    /// <param name="targetBounds">目标区域的边界矩形。</param>
    public void PositionAtTopRight(Drawing.Rectangle targetBounds)
    {
        // 计算窗口新位置：右对齐目标区域右边，留24像素边距；顶部对齐目标区域顶部，留24像素边距
        _boundsPx = new Drawing.Rectangle(
            targetBounds.Right - (int)Width - 24,
            targetBounds.Top + 24,
            (int)Width,
            (int)Height);
        // 如果窗口句柄已初始化，则应用新边界
        if (_hwnd != IntPtr.Zero)
        {
            Win32Native.SetWindowBoundsPixels(_hwnd, _boundsPx);
        }
    }

    /// <summary>
    /// 更新显示的消息和倒计时文本。
    /// </summary>
    /// <param name="message">要显示的消息文本。</param>
    /// <param name="autoActionText">自动操作的文本模板，包含占位符。</param>
    /// <param name="remaining">剩余时间。</param>
    public void UpdateMessage(string message, string autoActionText, TimeSpan remaining)
    {
        MessageText.Text = message; // 设置消息文本
        // 计算剩余秒数，确保非负，并向上取整
        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        // 使用模板格式化倒计时文本，替换占位符为秒数
        CountdownText.Text = string.Format(autoActionText, seconds);
    }

    // 鼠标按下事件处理程序
/// <summary>
    /// 处理鼠标按下事件，触发取消请求并隐藏窗口。
    /// </summary>
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty); // 触发取消请求事件
        Hide(); // 隐藏窗口
    }
}
