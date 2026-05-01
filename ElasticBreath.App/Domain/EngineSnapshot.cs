namespace ElasticBreath.App.Domain;

public sealed record PendingTransitionSnapshot(
    PendingTransitionKind Kind,
    string MessageKey,
    TimeSpan Remaining);

/// <summary>智能检测的探测进度，用于 UI 显示"还需持续 X 秒才能触发切换"</summary>
public sealed record DetectionProbeSnapshot(
    string MessageKey,
    TimeSpan Elapsed,
    TimeSpan Required);

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
    DateTimeOffset UpdatedAt)
{
    public double WorkingProgressRatio(TimeSpan maxWork)
        => maxWork <= TimeSpan.Zero ? 0 : Math.Clamp(WorkingCycleElapsed.TotalSeconds / maxWork.TotalSeconds, 0, 1);

    public double RestingProgressRatio(TimeSpan overtime)
        => overtime <= TimeSpan.Zero ? 0 : Math.Clamp(RestingCycleElapsed.TotalSeconds / overtime.TotalSeconds, 0, 1);
}
