using System.Drawing;
using ElasticBreath.App.Interop;
using Forms = System.Windows.Forms;

namespace ElasticBreath.App.Services;

/// <summary>
/// 显示目标服务类，用于管理屏幕显示、光标位置检测以及全屏前台窗口判断等操作。
/// </summary>
public sealed class DisplayTargetService
{
    public Forms.Screen GetTargetScreen(string preferredDisplayId)
    {
        // 获取所有可用屏幕
        var screens = Forms.Screen.AllScreens;
        // 如果没有屏幕，则返回主屏幕或第一个屏幕作为默认
/// <summary>
        /// 当screens数组为空时，返回主屏幕或所有屏幕中的第一个。
        /// </summary>
        // 检查screens数组长度是否为0
        if (screens.Length == 0)
        {
            // 返回主屏幕，如果主屏幕为null则返回所有屏幕中的第一个
            return Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        }

        // 如果 preferredDisplayId 不是 "auto"（忽略大小写），则尝试匹配指定设备名称
/// <summary>
/// 根据首选显示ID查找匹配的屏幕。
/// </summary>
/// <param name="preferredDisplayId">首选显示设备的标识符。</param>
/// <param name="screens">可用的屏幕列表。</param>
/// <returns>如果找到匹配的屏幕则返回该屏幕对象，否则返回null。</returns>
        // 检查首选显示ID是否不是自动模式（不区分大小写）
        if (!string.Equals(preferredDisplayId, "auto", StringComparison.OrdinalIgnoreCase))
        {
            // 在屏幕列表中查找设备名称匹配的屏幕（不区分大小写）
            var matched = screens.FirstOrDefault(x => string.Equals(x.DeviceName, preferredDisplayId, StringComparison.OrdinalIgnoreCase));
            // 如果找到了匹配的屏幕
            if (matched is not null)
            {
                // 返回找到的匹配屏幕
                return matched;
            }
        }

        // 如果未指定或匹配失败，则查找主屏幕
        var primary = screens.FirstOrDefault(x => x.Primary);
/// <summary>
/// 判断主对象是否为空，如果不为空则返回主对象。
/// </summary>
        if (primary is not null)
        {
            // 如果主对象不为空，则直接返回主对象
            return primary;
        }

        // 最后，基于当前光标位置返回所在屏幕
        var cursor = Forms.Cursor.Position;
        return Forms.Screen.FromPoint(cursor);
    }

    /// <summary>
    /// 检查当前光标位置是否位于指定屏幕的边界内。
    /// </summary>
    public bool IsCursorOnScreen(Forms.Screen screen)
    {
        // 获取当前光标位置，并检查是否在指定屏幕的边界内
        var cursor = Forms.Cursor.Position;
        return screen.Bounds.Contains(cursor);
    }

    /// <summary>
    /// 检查前台窗口是否全屏覆盖指定屏幕。
    /// </summary>
    /// <param name="targetScreen">目标屏幕</param>
    /// <param name="ignoredWindow">忽略的窗口句柄</param>
    /// <returns>如果前台窗口全屏覆盖指定屏幕，则返回true；否则返回false</returns>
    public bool IsFullscreenForeground(Forms.Screen targetScreen, IntPtr ignoredWindow)
    {
        // 获取前台窗口句柄
        var hwnd = Win32Native.GetForegroundWindow();
        // 如果句柄无效或与忽略窗口相同，则返回 false
        if (hwnd == IntPtr.Zero || hwnd == ignoredWindow)
        {
            return false;
        }

        // 尝试获取前台窗口的矩形区域，失败则返回 false
        if (!Win32Native.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        // 将窗口矩形转换为 Rectangle 对象
        var fgRect = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        // 获取目标屏幕的矩形区域
        var screenRect = targetScreen.Bounds;
        // 设置容差值，用于判断窗口是否近似全屏覆盖
        const int tolerance = 2;

        // 检查前台窗口矩形是否覆盖整个目标屏幕，考虑容差范围
        return fgRect.Left <= screenRect.Left + tolerance
            && fgRect.Top <= screenRect.Top + tolerance
            && fgRect.Right >= screenRect.Right - tolerance
            && fgRect.Bottom >= screenRect.Bottom - tolerance;
    }
}
