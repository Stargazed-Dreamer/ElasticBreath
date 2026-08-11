using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ElasticBreath.App.Services;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace ElasticBreath.App.UI;

/// <summary>
/// 关于窗口：展示版本、作者、仓库链接与许可证，并提供“复制版本信息”与
/// “打开崩溃日志文件夹”两个便捷入口。所有元数据在窗口构造时从磁盘实时读取。
/// </summary>
public partial class AboutWindow : Window
{
    private readonly LocalizationService _localization;
    private readonly AppMeta _meta;

    /// <summary>
    /// 构造函数。
    /// </summary>
    /// <param name="localization">本地化服务，用于翻译标签与按钮文案。</param>
    /// <param name="metaService">应用元数据服务，从 <c>app.meta.json</c> 读取版本/作者/仓库等。</param>
    public AboutWindow(LocalizationService localization, AppMetaService metaService)
    {
        InitializeComponent();
        _localization = localization;
        _meta = metaService.Load();

        Title = _localization.T("about.title");
        AppNameText.Text = _localization.T("app.name");
        VersionText.Text = string.IsNullOrEmpty(_meta.Version)
            ? _localization.T("about.version_unknown")
            : _localization.Tf("about.version_format", _meta.Version);

        // 作者行
        AuthorLabel.Text = _localization.T("about.author");
        if (string.IsNullOrEmpty(_meta.Authors))
        {
            AuthorRow.Visibility = Visibility.Collapsed;
        }
        else
        {
            AuthorValue.Text = _meta.Authors;
        }

        // 仓库行（超链接 + 复制按钮）
        RepoLabel.Text = _localization.T("about.repository");
        if (string.IsNullOrEmpty(_meta.Repository))
        {
            RepoRow.Visibility = Visibility.Collapsed;
        }
        else
        {
            RepoLink.NavigateUri = new Uri(_meta.Repository);
            RepoLink.Inlines.Add(_meta.Repository);
            CopyRepoButton.Content = _localization.T("about.copy_repo");
        }

        // 许可证行
        LicenseLabel.Text = _localization.T("about.license");
        if (string.IsNullOrEmpty(_meta.License))
        {
            LicenseRow.Visibility = Visibility.Collapsed;
        }
        else
        {
            LicenseValue.Text = _meta.License;
        }

        CopyVersionButton.Content = _localization.T("about.copy_version");
        OpenCrashLogButton.Content = _localization.T("about.open_crash_log");
        CloseButton.Content = _localization.T("about.close");

        LoadAppIcon();
    }

    /// <summary>
    /// 从输出目录下的 <c>Resource/icon.ico</c> 读取图标，选取其中像素宽度最大（最清晰）的帧，
    /// 赋给 <see cref="AppIcon"/> 显示。XAML 中已设 <c>Stretch="Uniform"</c>，保证按比例缩放到
    /// 56×56 容器内不裁切；<c>BitmapScalingMode="HighQuality"</c> 保证缩小采样质量。
    /// 文件缺失或读取失败时静默，不影响窗口其他功能。
    /// </summary>
    private void LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resource", "icon.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        try
        {
            var decoder = BitmapDecoder.Create(
                new Uri(iconPath),
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.OnLoad);

            BitmapFrame? best = null;
            foreach (var frame in decoder.Frames)
            {
                if (best is null || frame.PixelWidth > best.PixelWidth)
                {
                    best = frame;
                }
            }

            if (best is not null)
            {
                AppIcon.Source = best;
            }
        }
        catch
        {
            // 图标加载失败不影响窗口其他功能。
        }
    }

    /// <summary>
    /// 仓库超链接被点击时，委托给系统默认浏览器打开。程序自身不发起 HTTP 请求，
    /// 仅以 <see cref="ProcessStartInfo.UseShellExecute"/> = true 交给系统 shell 处理。
    /// </summary>
    private void RepoLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // 启动浏览器失败时静默；用户可改用“复制仓库地址”按钮手动粘贴到浏览器。
        }
        e.Handled = true;
    }

    /// <summary>
    /// 复制仓库链接到剪贴板。使用 WPF <see cref="Clipboard"/> API，符合 AGENTS.md
    /// “剪贴板能力走 WPF 等价 API”的约定，不引入 WinForms 依赖。
    /// </summary>
    private void CopyRepoButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_meta.Repository))
        {
            return;
        }

        try
        {
            Clipboard.SetText(_meta.Repository);
            MessageBox.Show(this, _localization.T("about.repo_copied"), _localization.T("about.title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch
        {
            // 剪贴板被其他进程独占等场景静默处理，不打扰用户。
        }
    }

    /// <summary>
    /// 复制完整版本信息到剪贴板，便于用户在反馈 bug 时附上版本/OS/运行时上下文。
    /// </summary>
    private void CopyVersionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(BuildVersionInfoText());
            MessageBox.Show(this, _localization.T("about.version_info_copied"), _localization.T("about.title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch
        {
            // 同上静默处理。
        }
    }

    /// <summary>
    /// 在系统资源管理器中打开崩溃日志文件夹。目录不存在时提示用户。
    /// 路径与 <see cref="CrashLogger"/> 写入位置一致。
    /// </summary>
    private void OpenCrashLogButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = AppMetaService.CrashLogDirectory;
        if (!Directory.Exists(dir))
        {
            MessageBox.Show(this, _localization.T("about.crash_log_missing"), _localization.T("about.title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show(this, _localization.T("about.crash_log_open_failed"), _localization.T("about.title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// 构造用于复制的多行版本信息文本。
    /// 使用 <see cref="StringBuilder"/> 的 Append/AppendLine(string) 重载而非插值字符串，
    /// 避免 CA1305（区域设置相关的 AppendLine(ref AppendInterpolatedStringHandler) 警告）。
    /// </summary>
    private string BuildVersionInfoText()
    {
        var sb = new StringBuilder();
        sb.Append(_localization.T("app.name")).Append(' ').AppendLine(_meta.Version);
        if (!string.IsNullOrEmpty(_meta.Authors))
        {
            sb.Append(_localization.T("about.author")).Append(": ").AppendLine(_meta.Authors);
        }
        if (!string.IsNullOrEmpty(_meta.Repository))
        {
            sb.Append(_localization.T("about.repository")).Append(": ").AppendLine(_meta.Repository);
        }
        if (!string.IsNullOrEmpty(_meta.License))
        {
            sb.Append(_localization.T("about.license")).Append(": ").AppendLine(_meta.License);
        }
        sb.Append("OS: ").AppendLine(Environment.OSVersion.ToString());
        sb.Append("Runtime: .NET ").AppendLine(Environment.Version.ToString());
        return sb.ToString();
    }
}
