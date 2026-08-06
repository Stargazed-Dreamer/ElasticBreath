using System.Text;
using System.Windows;
using ElasticBreath.App.Services;

namespace ElasticBreath.App.UI;

/// <summary>
/// 崩溃恢复提示窗口。
/// 应用启动时若检测到上次崩溃遗留的日志，弹出此窗口展示崩溃详情，
/// 用户可复制日志用于反馈，或确认后清除日志。
/// 设计参考：design.md §7（"下次启动提示"）。
/// </summary>
public partial class CrashRecoveryWindow : Window
{
    private readonly IReadOnlyList<CrashLogEntry> _entries;

    /// <param name="localization">本地化服务</param>
    /// <param name="entries">待展示的崩溃日志条目</param>
    public CrashRecoveryWindow(LocalizationService localization, IReadOnlyList<CrashLogEntry> entries)
    {
        InitializeComponent();
        _entries = entries;

        Title = localization.T("crash.title");
        HeadingText.Text = localization.T("crash.heading");
        DescriptionText.Text = localization.Tf("crash.description", entries.Count);
        CopyButton.Content = localization.T("crash.copy");
        DismissButton.Content = localization.T("crash.dismiss");

        DetailTextBox.Text = BuildDetailText();
    }

    /// <summary>将所有崩溃条目拼接为可读文本。</summary>
    private string BuildDetailText()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (i > 0)
            {
                sb.AppendLine();
                sb.AppendLine("────────────────────────────────────────");
                sb.AppendLine();
            }
            sb.AppendLine($"# {i + 1}/{_entries.Count}");
            sb.AppendLine($"文件: {e.FilePath}");
            sb.AppendLine($"时间: {(e.Timestamp == DateTime.MinValue ? "未知" : e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"))}");
            sb.AppendLine($"来源: {e.Source}");
            sb.AppendLine($"消息: {e.Message}");
            sb.AppendLine();
            sb.AppendLine(e.FullContent);
        }
        return sb.ToString();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(BuildDetailText());
        }
        catch
        {
            // 某些环境剪贴板不可用，忽略
        }
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        // 用户确认后清除所有崩溃日志，避免下次启动重复提示
        CrashRecoveryService.DeleteAll();
        Close();
    }
}
