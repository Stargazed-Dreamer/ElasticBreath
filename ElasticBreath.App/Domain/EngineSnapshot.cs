namespace ElasticBreath.App.Domain;

/// <summary>
/// 记录待处理状态切换的快照，包含切换类型、消息键和剩余时间。
/// </summary>
public sealed record PendingTransitionSnapshot(
    PendingTransitionKind Kind,
    string MessageKey,
    TimeSpan Remaining);

/// <summary>智能检测的探测进度，用于 UI 显示"还需持续 X 秒才能触发切换"</summary>
public sealed record DetectionProbeSnapshot(
    string MessageKey,
    TimeSpan Elapsed,
    TimeSpan Required);

/// <summary>
/// 弹性呼吸引擎的完整状态快照，用于 UI 显示和状态持久化。
/// 包含呼吸状态、压力水平、循环进度、历史统计和控制标志等信息。
/// </summary>
public sealed record EngineSnapshot(
    ElasticBreathState State,
    WorkingPressureLevel WorkingPressure,
    RestPressureLevel RestPressure,
    TimeSpan WorkingCycleElapsed,
    TimeSpan RestingCycleElapsed,
    TimeSpan TotalWorkingToday,
    TimeSpan TotalRestingToday,
    PendingTransitionSnapshot? PendingTransition,
    DetectionProbeSnapshot? DetectionProbe,
    bool RemindersPaused,
    bool SessionLocked,
    ElasticBreathState StateBeforePause,
    DateTimeOffset UpdatedAt)
{
    /// <summary>
    /// 计算工作循环的进度比率。
    /// </summary>
    /// <param name="maxWork">最大允许的工作时长，作为进度计算的基准。</param>
    /// <returns>一个介于0到1之间的比率，表示当前工作时长占最大工作时长的比例。</returns>
    public double WorkingProgressRatio(TimeSpan maxWork)
        // 如果最大工作时长为0或负数，则返回0避免除以零错误
        => maxWork <= TimeSpan.Zero ? 0 : Math.Clamp(WorkingCycleElapsed.TotalSeconds / maxWork.TotalSeconds, 0, 1);

    /// <summary>
    /// 计算休息循环的进度比率。
    /// </summary>
    /// <param name="overtime">超时时长阈值，作为进度计算的基准。</param>
    /// <returns>一个介于0到1之间的比率，表示当前休息时长占超时时长阈值的比例。</returns>
    public double RestingProgressRatio(TimeSpan overtime)
        // 如果超时时长阈值为0或负数，则返回0避免除以零错误
        => overtime <= TimeSpan.Zero ? 0 : Math.Clamp(RestingCycleElapsed.TotalSeconds / overtime.TotalSeconds, 0, 1);
}
