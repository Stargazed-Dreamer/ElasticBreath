namespace ElasticBreath.App.Domain;

public enum CloseBehavior
{
    Exit = 0,
    MinimizeToTray = 1
}

public sealed class ElasticBreathSettings
{
    public const int MinWorkSecondsMin = 1;
    public const int MinWorkSecondsMax = 7200;
    public const int MaxWorkSecondsMin = 1;
    public const int MaxWorkSecondsMax = 14400;
    public const int DefaultRestSecondsMin = 1;
    public const int DefaultRestSecondsMax = 7200;
    public const int RestOvertimeSecondsMin = 1;
    public const int RestOvertimeSecondsMax = 14400;
    public const int MinEffectiveRestSecondsMin = 1;
    public const int MinEffectiveRestSecondsMax = 7200;
    public const int AwayThresholdSecondsMin = 1;
    public const int AwayThresholdSecondsMax = 7200;
    public const int AutoRestAfterIdleSecondsMin = 10;
    public const int AutoRestAfterIdleSecondsMax = 3600;
    public const int IdleToWorkDetectSecondsMin = 1;
    public const int IdleToWorkDetectSecondsMax = 600;
    public const int RestToWorkDetectSecondsMin = 1;
    public const int RestToWorkDetectSecondsMax = 600;
    public const int SmartDetectGapSecondsMin = 1;
    public const int SmartDetectGapSecondsMax = 15;
    public const int AutoTransitionCountdownSecondsMin = 1;
    public const int AutoTransitionCountdownSecondsMax = 120;
    public const double CornerHoverSecondsMin = 0.5;
    public const double CornerHoverSecondsMax = 10;

    public string Language { get; set; } = "zh-CN";
    public CloseBehavior CloseBehaviorOnMainWindowClose { get; set; } = CloseBehavior.MinimizeToTray;

    public int MinWorkSeconds { get; set; } = 35 * 60;
    public int MaxWorkSeconds { get; set; } = 45 * 60;
    public int DefaultRestSeconds { get; set; } = 5 * 60;
    public int RestOvertimeSeconds { get; set; } = 8 * 60;
    public int MinEffectiveRestSeconds { get; set; } = 3 * 60;
    public int AwayThresholdSeconds { get; set; } = 3 * 60;
    public int AutoRestAfterIdleSeconds { get; set; } = 30;
    public int IdleToWorkDetectSeconds { get; set; } = 4;
    public int RestToWorkDetectSeconds { get; set; } = 30;
    public int SmartDetectGapSeconds { get; set; } = 2;
    public int AutoTransitionCountdownSeconds { get; set; } = 5;
    public double CornerHoverSeconds { get; set; } = 1.5;

    public bool EnableTopProgressBar { get; set; }
    public bool EnableEdgeGlow { get; set; } = true;
    public bool EnableCornerHover { get; set; } = true;
    public int GlowMaxThicknessPixels { get; set; } = 80;
    public double OverlayOpacity { get; set; } = 0.35;
    public bool FullscreenHideMode { get; set; }
    public bool EnableSecondaryMonitorFlash { get; set; } = true;

    /* 周期性重新置顶，防止 Win+D 等操作将悬浮层推到后面 */
    public bool EnablePeriodicReTopmost { get; set; }
    public int ReTopmostIntervalSeconds { get; set; } = 5;
    public const int ReTopmostIntervalSecondsMin = 1;
    public const int ReTopmostIntervalSecondsMax = 60;

    public bool EnableSound { get; set; }
    public int ReminderVolumePercent { get; set; } = 50;
    public bool EnableFullscreenFallbackBeep { get; set; }

    public string PreferredDisplay { get; set; } = "auto";

    /* 用户在设置界面输入的原始表达式文本（如 "35*60"），保存到 JSON 时使用原始文本以方便阅读 */
    public Dictionary<string, string> RawExpressions { get; set; } = new();

    public TimeSpan MinWorkThreshold => TimeSpan.FromSeconds(MinWorkSeconds);
    public TimeSpan MaxWorkThreshold => TimeSpan.FromSeconds(MaxWorkSeconds);
    public TimeSpan DefaultRestThreshold => TimeSpan.FromSeconds(DefaultRestSeconds);
    public TimeSpan RestOvertimeThreshold => TimeSpan.FromSeconds(RestOvertimeSeconds);
    public TimeSpan MinEffectiveRestThreshold => TimeSpan.FromSeconds(MinEffectiveRestSeconds);
    public TimeSpan AwayThreshold => TimeSpan.FromSeconds(AwayThresholdSeconds);
    public TimeSpan AutoRestAfterIdleThreshold => TimeSpan.FromSeconds(AutoRestAfterIdleSeconds);
    public TimeSpan IdleToWorkDetectThreshold => TimeSpan.FromSeconds(IdleToWorkDetectSeconds);
    public TimeSpan RestToWorkDetectThreshold => TimeSpan.FromSeconds(RestToWorkDetectSeconds);
    public TimeSpan SmartDetectGapThreshold => TimeSpan.FromSeconds(SmartDetectGapSeconds);
    public TimeSpan AutoTransitionCountdown => TimeSpan.FromSeconds(AutoTransitionCountdownSeconds);
    public TimeSpan CornerHoverDuration => TimeSpan.FromSeconds(CornerHoverSeconds);

    public ElasticBreathSettings Sanitize()
    {
        MinWorkSeconds = Math.Clamp(MinWorkSeconds, MinWorkSecondsMin, MinWorkSecondsMax);
        MaxWorkSeconds = Math.Clamp(MaxWorkSeconds, Math.Max(MinWorkSeconds, MaxWorkSecondsMin), MaxWorkSecondsMax);
        DefaultRestSeconds = Math.Clamp(DefaultRestSeconds, DefaultRestSecondsMin, DefaultRestSecondsMax);
        RestOvertimeSeconds = Math.Clamp(RestOvertimeSeconds, Math.Max(DefaultRestSeconds, RestOvertimeSecondsMin), RestOvertimeSecondsMax);
        MinEffectiveRestSeconds = Math.Clamp(MinEffectiveRestSeconds, MinEffectiveRestSecondsMin, Math.Min(MinEffectiveRestSecondsMax, RestOvertimeSeconds));
        AwayThresholdSeconds = Math.Clamp(AwayThresholdSeconds, Math.Max(RestOvertimeSeconds + 1, AwayThresholdSecondsMin), AwayThresholdSecondsMax);
        AutoRestAfterIdleSeconds = Math.Clamp(AutoRestAfterIdleSeconds, AutoRestAfterIdleSecondsMin, AutoRestAfterIdleSecondsMax);
        IdleToWorkDetectSeconds = Math.Clamp(IdleToWorkDetectSeconds, IdleToWorkDetectSecondsMin, IdleToWorkDetectSecondsMax);
        RestToWorkDetectSeconds = Math.Clamp(RestToWorkDetectSeconds, RestToWorkDetectSecondsMin, RestToWorkDetectSecondsMax);
        SmartDetectGapSeconds = Math.Clamp(SmartDetectGapSeconds, SmartDetectGapSecondsMin, SmartDetectGapSecondsMax);
        AutoTransitionCountdownSeconds = Math.Clamp(AutoTransitionCountdownSeconds, AutoTransitionCountdownSecondsMin, AutoTransitionCountdownSecondsMax);
        CornerHoverSeconds = Math.Clamp(CornerHoverSeconds, CornerHoverSecondsMin, CornerHoverSecondsMax);
        GlowMaxThicknessPixels = Math.Clamp(GlowMaxThicknessPixels, 12, 600);
        OverlayOpacity = Math.Clamp(OverlayOpacity, 0.1, 0.9);
        ReminderVolumePercent = Math.Clamp(ReminderVolumePercent, 0, 100);
        ReTopmostIntervalSeconds = Math.Clamp(ReTopmostIntervalSeconds, ReTopmostIntervalSecondsMin, ReTopmostIntervalSecondsMax);
        PreferredDisplay ??= "auto";
        Language = string.IsNullOrWhiteSpace(Language) ? "zh-CN" : Language;
        return this;
    }
}
