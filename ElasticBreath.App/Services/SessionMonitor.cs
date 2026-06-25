using Microsoft.Win32;

namespace ElasticBreath.App.Services;

/// <summary>
/// 监控 Windows 会话锁定和解锁状态变化的类。
/// 实现了 IDisposable 接口，以确保在对象销毁时正确取消事件订阅。
/// </summary>
public sealed class SessionMonitor : IDisposable
{
    /// <summary>
    /// 初始化会话监视器，订阅系统会话切换事件以捕获锁定和解锁操作。
    /// </summary>
    public SessionMonitor()
    {
        // 订阅系统会话切换事件，以捕获锁定和解锁操作
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>
    /// 当会话锁定状态改变时触发的事件。
    /// 事件参数的布尔值表示会话是否被锁定（true 表示锁定，false 表示解锁）。
    /// </summary>
    public event EventHandler<bool>? SessionLockChanged;

/// <summary>
    /// 释放资源，取消订阅系统事件以防止内存泄漏。
    /// </summary>
    public void Dispose()
    {
        // 取消订阅系统会话切换事件，防止内存泄漏和不必要的事件处理
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

/// <summary>
    /// 处理会话切换事件的方法。
    /// 当会话被锁定或解锁时，触发相应的事件通知。
    /// </summary>
    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        // 如果会话被锁定
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            // 触发事件，通知订阅者会话已锁定（参数为 true）
            SessionLockChanged?.Invoke(this, true);
            return;
        }

        // 如果会话被解锁
        if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            // 触发事件，通知订阅者会话已解锁（参数为 false）
            SessionLockChanged?.Invoke(this, false);
        }
    }
}
