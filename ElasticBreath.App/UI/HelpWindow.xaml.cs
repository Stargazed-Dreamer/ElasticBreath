using System.Windows;
using System.Windows.Documents;
using ElasticBreath.App.Services;

namespace ElasticBreath.App.UI;

/// <summary>
/// 帮助窗口：以 FlowDocument 渲染功能说明，标题段加粗着色，正文段常规，
/// 通过解析 i18n 的 help.body 文本（以 \n 分行、以【...】或 [...] 开头识别为小标题）生成段落。
/// </summary>
public partial class HelpWindow : Window
{
    private const string HeadingColor = "#FF1C4C35";
    private const string BodyColor = "#FF1E3F30";

    /// <summary>
    /// 构造函数，初始化帮助窗口并填充本地化文本。
    /// </summary>
    /// <param name="localization">本地化服务实例。</param>
    public HelpWindow(LocalizationService localization)
    {
        InitializeComponent();
        Title = localization.T("help.title");
        HeadingText.Text = localization.T("help.heading");
        CloseButton.Content = localization.T("help.close");

        RenderBody(localization.T("help.body"));
    }

    /// <summary>
    /// 将 help.body 文本渲染为 FlowDocument 段落。
    /// 规则：按 \n 拆行；以【...】或 [...] 开头的行视为小标题，加粗加深色；
    /// 空行作为段落分隔；以“·”或“-”开头的行保持原样（列表项）；其余为正文。
    /// </summary>
    private void RenderBody(string body)
    {
        Doc.Blocks.Clear();
        var lines = body.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            var p = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 6),
                LineHeight = 20
            };

            if (IsHeading(line))
            {
                p.Inlines.Add(new Run(line)
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(HeadingColor))
                });
                p.Margin = new Thickness(0, 8, 0, 4);
            }
            else
            {
                p.Inlines.Add(new Run(line)
                {
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(BodyColor))
                });
            }

            Doc.Blocks.Add(p);
        }
    }

    /// <summary>
    /// 判断一行是否为小标题：以【...】或 [...] 包裹开头，或以“数字.”开头但非列表项。
    /// </summary>
    private static bool IsHeading(string line)
    {
        var t = line.TrimStart();
        if (t.StartsWith('【') || t.StartsWith('['))
        {
            return true;
        }
        return false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
