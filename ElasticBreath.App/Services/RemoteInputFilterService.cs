using System.Diagnostics;
using ElasticBreath.App.Interop;

namespace ElasticBreath.App.Services;

public sealed class RemoteInputFilterService
{
    private static readonly HashSet<string> KnownRemoteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "todesk",
        "raylink",
        "sunloginclient",
        "sunlogin",
        "teamviewer",
        "anydesk",
        "rustdesk"
    };

    public bool IsLikelyRemoteControlForeground()
    {
        var hwnd = Win32Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        _ = Win32Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return false;
        }

        try
        {
            var process = Process.GetProcessById((int)pid);
            var name = process.ProcessName;
            return KnownRemoteTools.Contains(name);
        }
        catch
        {
            return false;
        }
    }
}
