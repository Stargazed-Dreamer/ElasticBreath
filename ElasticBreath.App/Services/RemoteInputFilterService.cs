using System.Diagnostics;
using ElasticBreath.App.Interop;

namespace ElasticBreath.App.Services;

/// <summary>
/// 远程输入过滤服务，用于检测当前前台窗口是否属于已知的远程控制软件。
/// </summary>
public sealed class RemoteInputFilterService
{
    // 存储已知远程控制工具的进程名称集合，使用不区分大小写的比较器
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

    /// <summary>
    /// 判断当前前台窗口是否很可能由远程控制软件控制。
    /// </summary>
    /// <returns>如果前台窗口的进程名称在已知远程工具列表中，则返回 true；否则返回 false。</returns>
    public bool IsLikelyRemoteControlForeground()
    {
        // 获取前台窗口的句柄
        var hwnd = Win32Native.GetForegroundWindow();
        // 如果获取失败（句柄为零），则返回 false
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        // 根据窗口句柄获取对应的进程ID
        _ = Win32Native.GetWindowThreadProcessId(hwnd, out var pid);
        // 如果进程ID为0，表示获取失败，返回 false
        if (pid == 0)
        {
            return false;
        }

        try
        {
            // 根据进程ID获取进程对象
            var process = Process.GetProcessById((int)pid);
            // 获取进程的名称
            var name = process.ProcessName;
            // 检查进程名称是否存在于已知远程工具列表中
            return KnownRemoteTools.Contains(name);
        }
        catch
        {
            // 如果在获取进程信息过程中发生异常（如进程已退出），则返回 false
            return false;
        }
    }
}
