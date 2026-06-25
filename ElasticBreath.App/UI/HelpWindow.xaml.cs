using System.Windows;
using ElasticBreath.App.Services;

namespace ElasticBreath.App.UI;

/// <summary>
/// 帮助窗口类，继承自Window，用于显示应用程序的帮助信息。
/// </summary>
public partial class HelpWindow : Window
{
    /// <summary>
    /// 构造函数，初始化帮助窗口并设置本地化文本。
    /// </summary>
    /// <param name="localization">本地化服务实例，用于获取翻译文本。</param>
    public HelpWindow(LocalizationService localization)
    {
/// <summary>
/// 初始化UI组件，设置窗口及各控件的本地化文本。
/// </summary>
        InitializeComponent(); // 初始化UI组件
        Title = localization.T("help.title"); // 设置窗口标题为本地化的帮助标题
        HeadingText.Text = localization.T("help.heading"); // 设置标题文本为本地化的帮助标题
        BodyText.Text = localization.T("help.body"); // 设置正文文本为本地化的帮助内容
        CloseButton.Content = localization.T("help.close"); // 设置关闭按钮内容为本地化的关闭文本
    }

    /// <summary>
    /// 关闭按钮点击事件处理程序，用于关闭帮助窗口。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">事件参数。</param>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close(); // 关闭当前窗口
    }
}
