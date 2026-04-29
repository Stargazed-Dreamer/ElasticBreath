using System.Drawing;
using ElasticBreath.App.Interop;
using Forms = System.Windows.Forms;

namespace ElasticBreath.App.Services;

public sealed class DisplayTargetService
{
    public Forms.Screen GetTargetScreen(string preferredDisplayId)
    {
        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0)
        {
            return Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        }

        if (!string.Equals(preferredDisplayId, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var matched = screens.FirstOrDefault(x => string.Equals(x.DeviceName, preferredDisplayId, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                return matched;
            }
        }

        var primary = screens.FirstOrDefault(x => x.Primary);
        if (primary is not null)
        {
            return primary;
        }

        var cursor = Forms.Cursor.Position;
        return Forms.Screen.FromPoint(cursor);
    }

    public bool IsCursorOnScreen(Forms.Screen screen)
    {
        var cursor = Forms.Cursor.Position;
        return screen.Bounds.Contains(cursor);
    }

    public bool IsFullscreenForeground(Forms.Screen targetScreen, IntPtr ignoredWindow)
    {
        var hwnd = Win32Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == ignoredWindow)
        {
            return false;
        }

        if (!Win32Native.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        var fgRect = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        var screenRect = targetScreen.Bounds;
        const int tolerance = 2;

        return fgRect.Left <= screenRect.Left + tolerance
            && fgRect.Top <= screenRect.Top + tolerance
            && fgRect.Right >= screenRect.Right - tolerance
            && fgRect.Bottom >= screenRect.Bottom - tolerance;
    }
}
