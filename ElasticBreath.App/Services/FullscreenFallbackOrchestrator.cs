using ElasticBreath.App.Interop;

namespace ElasticBreath.App.Services;

/// <summary>
/// 全屏回退编排服务。
/// 当目标屏幕被全屏前台应用占据、边缘光晕被隐藏时，回退到：
/// 1. 任务栏图标闪烁（FlashWindowEx，持续到窗口进入前台）；
/// 2. 每隔 5 秒播放一次短促提示音（受 EnableFullscreenFallbackBeep 开关控制）。
/// 仅在"存在需提醒状态"时触发，避免在全屏观影等无提醒场景打扰用户。
/// 设计参考：design.md §7（"回退到任务栏图标闪烁 + 可选的每隔5秒短促提示音"）。
/// </summary>
public sealed class FullscreenFallbackOrchestrator
{
    /// <summary>回退提示音的间隔（秒），与 design.md §7 "每隔5秒" 一致</summary>
    private const double BeepIntervalSeconds = 5.0;

    private readonly SoundService _sound;
    private bool _active;
    private DateTime _lastBeepUtc = DateTime.MinValue;
    private IntPtr _flashedWindow = IntPtr.Zero;

    public FullscreenFallbackOrchestrator(SoundService sound)
    {
        _sound = sound;
    }

    /// <summary>
    /// 由主窗口在每个快照更新时调用，驱动回退行为。
    /// </summary>
    /// <param name="hideForFullscreen">悬浮层是否因全屏应用而被隐藏</param>
    /// <param name="hasActiveReminder">当前是否存在需要提醒的状态（工作预警/硬性 或 休息超时）</param>
    /// <param name="windowHandle">用于闪烁的任务栏所属窗口句柄（主窗口）</param>
    public void Update(bool hideForFullscreen, bool hasActiveReminder, IntPtr windowHandle)
    {
        var shouldFallback = hideForFullscreen && hasActiveReminder;

        if (shouldFallback)
        {
            if (!_active)
            {
                // 进入回退模式：立即闪烁 + 立即响一声
                _flashedWindow = windowHandle;
                Win32Native.FlashTaskbar(windowHandle, start: true);
                _sound.PlayFallbackBeep();
                _lastBeepUtc = DateTime.UtcNow;
                _active = true;
            }
            else
            {
                // 已在回退模式：每隔 5 秒重复提示音并再次触发闪烁
                if ((DateTime.UtcNow - _lastBeepUtc).TotalSeconds >= BeepIntervalSeconds)
                {
                    _sound.PlayFallbackBeep();
                    Win32Native.FlashTaskbar(windowHandle, start: true);
                    _lastBeepUtc = DateTime.UtcNow;
                }
            }
        }
        else if (_active)
        {
            // 离开回退模式：停止闪烁
            Win32Native.FlashTaskbar(_flashedWindow, start: false);
            _flashedWindow = IntPtr.Zero;
            _active = false;
        }
    }

    /// <summary>重置内部状态（例如设置变更后）。</summary>
    public void Reset()
    {
        if (_active && _flashedWindow != IntPtr.Zero)
        {
            Win32Native.FlashTaskbar(_flashedWindow, start: false);
        }
        _active = false;
        _flashedWindow = IntPtr.Zero;
        _lastBeepUtc = DateTime.MinValue;
    }
}
