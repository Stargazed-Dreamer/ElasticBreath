using System.Windows;
using ElasticBreath.App.Services;

namespace ElasticBreath.App.UI;

public partial class HelpWindow : Window
{
    public HelpWindow(LocalizationService localization)
    {
        InitializeComponent();
        Title = localization.T("help.title");
        HeadingText.Text = localization.T("help.heading");
        BodyText.Text = localization.T("help.body");
        CloseButton.Content = localization.T("help.close");
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
