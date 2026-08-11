using ElasticBreath.App.Domain;
using ElasticBreath.App.UI;
using ElasticBreath.Rendering;
using Forms = System.Windows.Forms;

namespace ElasticBreath.App.Services;

/// <summary>
/// SecondaryMonitorFlashService 类用于管理副显示器的闪屏效果。
/// 它根据设置和状态控制副屏窗口的显示和闪烁，实现 IDisposable 接口以释放资源。
/// </summary>
public sealed class SecondaryMonitorFlashService : IDisposable
{
    private readonly Dictionary<string, EdgeOverlayWindow> _windows = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastFlashToggleUtc = DateTime.UtcNow;
    private bool _flashOn = true;

/// <summary>
    /// 更新副屏的边缘叠加层状态，控制闪屏行为和窗口可见性。
    /// </summary>
    /// <param name="settings">弹性呼吸设置对象，包含配置参数。</param>
    /// <param name="primaryScreen">主屏幕对象，用于标识主屏设备。</param>
    /// <param name="state">边缘叠加层状态，指定叠加层的显示状态。</param>
    /// <param name="suppressAll">是否抑制所有闪屏行为。</param>
    /// <param name="primaryScreenIgnored">是否忽略主屏幕。</param>
    public void Update(
        ElasticBreathSettings settings,
        Forms.Screen primaryScreen,
        EdgeOverlayState state,
        bool suppressAll,
        bool primaryScreenIgnored)
    {
        // 计算是否应该闪屏：条件包括启用副屏闪屏、未抑制所有、主屏被忽略且状态不是隐藏
        var shouldFlash = settings.EnableSecondaryMonitorFlash
            && !suppressAll
            && primaryScreenIgnored
            && state != EdgeOverlayState.Hidden;

        // 更新闪屏状态，基于时间间隔切换
        TickFlash();

        // 遍历所有屏幕，跳过主屏
        foreach (var screen in Forms.Screen.AllScreens)
        {
            // 检查是否是主屏设备，如果是则跳过处理
            if (string.Equals(screen.DeviceName, primaryScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 获取或创建副屏窗口
            var window = GetOrCreate(screen.DeviceName);
            // 设置窗口边界以匹配屏幕区域
            window.SetBounds(screen.Bounds);
            /* 副屏窗口也配置周期性重新置顶 */
            window.ConfigureReTopmost(settings.EnablePeriodicReTopmost, settings.ReTopmostIntervalSeconds);

            // 计算可见状态：如果应该闪屏且当前闪屏开启，则使用传入状态，否则隐藏
            var visibleState = shouldFlash && _flashOn ? state : EdgeOverlayState.Hidden;
            window.UpdateOverlay(
                visibleState,
                enableEdgeGlow: true,
                showTopProgress: false,
                topProgressRatio: 0,
                glowThickness: Math.Clamp(settings.GlowMaxThicknessPixels / 3, 8, 36),
                baseOpacity: settings.OverlayOpacity,
                hideAll: false);
        }

        // 如果应该闪屏，则提前返回，不执行后续隐藏操作
        if (shouldFlash)
        {
            return;
        }

        // 如果不应该闪屏，则隐藏所有副屏窗口
        foreach (var window in _windows.Values)
        {
            window.UpdateOverlay(
                EdgeOverlayState.Hidden,
                enableEdgeGlow: true,
                showTopProgress: false,
                topProgressRatio: 0,
                glowThickness: 8,
                baseOpacity: settings.OverlayOpacity,
                hideAll: true);
        }
    }

    // 释放资源：关闭所有窗口并清理字典
    /// <summary>
    /// 关闭并释放所有托管窗口资源。
    /// 此方法遍历所有已注册的窗口实例，逐个执行关闭操作，
    /// 并最终清空内部存储窗口引用的字典。
    /// </summary>
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

    // 更新闪屏状态，基于时间间隔切换开关
    /// <summary>
    /// 控制闪屏的切换。检查当前时间与上次切换时间的间隔，如果小于700毫秒则忽略，否则切换闪屏状态并更新时间戳。
    /// </summary>
    private void TickFlash()
    {
        var now = DateTime.UtcNow;
        // 如果距离上次切换时间小于700毫秒，则不执行切换
        if (now - _lastFlashToggleUtc < TimeSpan.FromMilliseconds(700))
        {
            return;
        }

        // 切换闪屏状态并更新时间戳
        _flashOn = !_flashOn;
        _lastFlashToggleUtc = now;
    }

    // 从字典获取窗口，如果不存在则创建新窗口
    /// <summary>
    /// 根据提供的键获取已存在的窗口实例，如果不存在则创建新实例并返回。
    /// </summary>
    /// <param name="key">用于标识和检索窗口的唯一字符串键。</param>
    /// <returns>与给定键关联的 <see cref="EdgeOverlayWindow"/> 实例。</returns>
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
