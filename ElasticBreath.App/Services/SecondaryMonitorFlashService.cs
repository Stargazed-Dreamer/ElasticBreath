using ElasticBreath.App.Domain;
using ElasticBreath.App.UI;
using ElasticBreath.Rendering;
using Forms = System.Windows.Forms;

namespace ElasticBreath.App.Services;

/// <summary>
/// 副屏呼吸提醒服务：主控屏由 MainWindow 的浮层负责呼吸，
/// 当鼠标不在主控屏上时，在鼠标实际所在的副屏上显示与主屏相同的呼吸边框，
/// 形成"主控屏 + 鼠标所在屏"双屏同时呼吸的效果。
/// 窗口按显示器 DeviceName 缓存，边界使用物理像素，配合 PerMonitorV2 精确覆盖对应显示器。
/// </summary>
public sealed class SecondaryMonitorFlashService : IDisposable
{
    private readonly Dictionary<string, EdgeOverlayWindow> _windows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 更新各副屏的呼吸边框状态。
    /// </summary>
    /// <param name="settings">弹性呼吸设置对象，包含配置参数。</param>
    /// <param name="primaryScreen">主控屏（主呼吸浮层所在屏）。</param>
    /// <param name="state">与主浮层一致的覆盖层状态。</param>
    /// <param name="suppressAll">是否因全屏隐藏等原因抑制所有副屏显示。</param>
    /// <param name="glowThickness">与主浮层一致的光晕厚度（像素）。</param>
    public void Update(
        ElasticBreathSettings settings,
        Forms.Screen primaryScreen,
        EdgeOverlayState state,
        bool suppressAll,
        int glowThickness)
    {
        var cursorScreen = Forms.Screen.FromPoint(Forms.Cursor.Position);

        // 遍历所有屏幕，跳过主控屏；仅鼠标当前所在的副屏跟随呼吸，其余副屏保持隐藏
        foreach (var screen in Forms.Screen.AllScreens)
        {
            if (string.Equals(screen.DeviceName, primaryScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var window = GetOrCreate(screen.DeviceName);
            window.SetBounds(screen.Bounds);
            /* 副屏窗口也配置周期性重新置顶 */
            window.ConfigureReTopmost(settings.EnablePeriodicReTopmost, settings.ReTopmostIntervalSeconds);

            var visibleState = ResolveVisibleState(
                settings.EnableSecondaryMonitorFlash,
                suppressAll,
                state,
                isCursorScreen: string.Equals(screen.DeviceName, cursorScreen.DeviceName, StringComparison.OrdinalIgnoreCase));

            window.UpdateOverlay(
                visibleState,
                enableEdgeGlow: true,
                showTopProgress: false,
                topProgressRatio: 0,
                glowThickness: glowThickness,
                baseOpacity: settings.OverlayOpacity,
                hideAll: false);
        }

        CloseStaleWindows();
    }

    /// <summary>
    /// 纯决策：某个副屏当前应显示的覆盖层状态（返回 Hidden 表示不显示）。
    /// 仅在功能开启、未被抑制、状态可见且该屏正是鼠标所在屏时显示呼吸。
    /// </summary>
    public static EdgeOverlayState ResolveVisibleState(
        bool enabled,
        bool suppressAll,
        EdgeOverlayState state,
        bool isCursorScreen)
    {
        if (!enabled || suppressAll || state == EdgeOverlayState.Hidden || !isCursorScreen)
        {
            return EdgeOverlayState.Hidden;
        }

        return state;
    }

    /// <summary>释放资源：关闭所有副屏窗口并清理字典。</summary>
    public void Dispose()
    {
        // 遍历所有窗口实例
        foreach (var window in _windows.Values)
        {
            window.Close(); // 关闭单个窗口
        }
        // 清空窗口字典，释放引用
        _windows.Clear();
    }

    /// <summary>关闭已从系统中移除的显示器对应的残留窗口，避免陈旧窗口滞留。</summary>
    private void CloseStaleWindows()
    {
        var currentDevices = Forms.Screen.AllScreens
            .Select(s => s.DeviceName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var staleKeys = _windows.Keys
            .Where(key => !currentDevices.Contains(key))
            .ToList();

        foreach (var key in staleKeys)
        {
            _windows[key].Close();
            _windows.Remove(key);
        }
    }

    // 从字典获取窗口，如果不存在则创建新窗口
    private EdgeOverlayWindow GetOrCreate(string key)
    {
        // 尝试从内部字典中获取与键关联的窗口
        if (_windows.TryGetValue(key, out var window))
        {
            // 如果找到，直接返回现有实例
            return window;
        }

        // 未找到现有窗口，创建一个新的实例
        window = new EdgeOverlayWindow();
        // 将新创建的窗口存储到字典中，以便后续使用相同键检索
        _windows[key] = window;
        return window;
    }
}
