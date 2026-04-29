using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ElasticBreath.App.Interop;
using Drawing = System.Drawing;

namespace ElasticBreath.App.UI;

public partial class CountdownNotificationWindow : Window
{
    private IntPtr _hwnd;
    private Drawing.Rectangle _boundsPx = new(0, 0, 420, 84);

    public CountdownNotificationWindow()
    {
        InitializeComponent();
        MouseDown += OnMouseDown;
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Win32Native.SetWindowBoundsPixels(_hwnd, _boundsPx);
        };
    }

    public event EventHandler? CancelRequested;

    public void PositionAtTopRight(Drawing.Rectangle targetBounds)
    {
        _boundsPx = new Drawing.Rectangle(
            targetBounds.Right - (int)Width - 24,
            targetBounds.Top + 24,
            (int)Width,
            (int)Height);
        if (_hwnd != IntPtr.Zero)
        {
            Win32Native.SetWindowBoundsPixels(_hwnd, _boundsPx);
        }
    }

    public void UpdateMessage(string message, string autoActionText, TimeSpan remaining)
    {
        MessageText.Text = message;
        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        CountdownText.Text = string.Format(autoActionText, seconds);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
        Hide();
    }
}
