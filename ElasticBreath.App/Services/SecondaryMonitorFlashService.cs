using ElasticBreath.App.Domain;
using ElasticBreath.App.UI;
using Forms = System.Windows.Forms;

namespace ElasticBreath.App.Services;

public sealed class SecondaryMonitorFlashService : IDisposable
{
    private readonly Dictionary<string, EdgeOverlayWindow> _windows = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastFlashToggleUtc = DateTime.UtcNow;
    private bool _flashOn = true;

    public void Update(
        ElasticBreathSettings settings,
        Forms.Screen primaryScreen,
        EdgeOverlayState state,
        bool suppressAll,
        bool primaryScreenIgnored)
    {
        var shouldFlash = settings.EnableSecondaryMonitorFlash
            && !suppressAll
            && primaryScreenIgnored
            && state != EdgeOverlayState.Hidden;

        TickFlash();

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

        if (shouldFlash)
        {
            return;
        }

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

    public void Dispose()
    {
        foreach (var window in _windows.Values)
        {
            window.Close();
        }
        _windows.Clear();
    }

    private void TickFlash()
    {
        var now = DateTime.UtcNow;
        if (now - _lastFlashToggleUtc < TimeSpan.FromMilliseconds(700))
        {
            return;
        }

        _flashOn = !_flashOn;
        _lastFlashToggleUtc = now;
    }

    private EdgeOverlayWindow GetOrCreate(string key)
    {
        if (_windows.TryGetValue(key, out var window))
        {
            return window;
        }

        window = new EdgeOverlayWindow();
        _windows[key] = window;
        return window;
    }
}
