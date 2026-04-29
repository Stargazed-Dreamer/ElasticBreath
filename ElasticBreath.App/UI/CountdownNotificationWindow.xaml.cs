using System.Windows;
using System.Windows.Input;

namespace ElasticBreath.App.UI;

public partial class CountdownNotificationWindow : Window
{
    public CountdownNotificationWindow()
    {
        InitializeComponent();
        MouseDown += OnMouseDown;
    }

    public event EventHandler? CancelRequested;

    public void PositionAtTopRight(Rect targetBounds)
    {
        Left = targetBounds.Right - Width - 24;
        Top = targetBounds.Top + 24;
    }

    public void UpdateMessage(string message, TimeSpan remaining)
    {
        MessageText.Text = message;
        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        CountdownText.Text = $"Auto action in {seconds}s. Click this card to cancel.";
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
        Hide();
    }
}
