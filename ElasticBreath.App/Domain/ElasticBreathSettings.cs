namespace ElasticBreath.App.Domain;

public sealed class ElasticBreathSettings
{
    public string Language { get; set; } = "zh-CN";

    public int MinWorkMinutes { get; set; } = 35;
    public int MaxWorkMinutes { get; set; } = 45;
    public int DefaultRestMinutes { get; set; } = 5;
    public int RestOvertimeMinutes { get; set; } = 8;
    public int MinEffectiveRestMinutes { get; set; } = 3;
    public int AwayThresholdMinutes { get; set; } = 3;
    public int AutoRestAfterIdleSeconds { get; set; } = 30;

    public int PostponeCooldownMinutes { get; set; } = 5;
    public int DailyPostponeLimit { get; set; } = 3;
    public int AutoTransitionCountdownSeconds { get; set; } = 5;
    public double CornerHoverSeconds { get; set; } = 1.5;

    public bool EnableTopProgressBar { get; set; }
    public bool EnableEdgeGlow { get; set; } = true;
    public bool EnableCornerHover { get; set; } = true;
    public int GlowMaxThicknessPixels { get; set; } = 80;
    public double OverlayOpacity { get; set; } = 0.35;
    public bool FullscreenHideMode { get; set; }
    public bool EnableSecondaryMonitorFlash { get; set; } = true;

    public bool EnableSound { get; set; }
    public int ReminderVolumePercent { get; set; } = 50;
    public bool EnableFullscreenFallbackBeep { get; set; }

    public string PreferredDisplay { get; set; } = "auto";

    public TimeSpan MinWorkThreshold => TimeSpan.FromMinutes(MinWorkMinutes);
    public TimeSpan MaxWorkThreshold => TimeSpan.FromMinutes(MaxWorkMinutes);
    public TimeSpan DefaultRestThreshold => TimeSpan.FromMinutes(DefaultRestMinutes);
    public TimeSpan RestOvertimeThreshold => TimeSpan.FromMinutes(RestOvertimeMinutes);
    public TimeSpan MinEffectiveRestThreshold => TimeSpan.FromMinutes(MinEffectiveRestMinutes);
    public TimeSpan AwayThreshold => TimeSpan.FromMinutes(AwayThresholdMinutes);
    public TimeSpan AutoRestAfterIdleThreshold => TimeSpan.FromSeconds(AutoRestAfterIdleSeconds);
    public TimeSpan PostponeCooldown => TimeSpan.FromMinutes(PostponeCooldownMinutes);
    public TimeSpan AutoTransitionCountdown => TimeSpan.FromSeconds(AutoTransitionCountdownSeconds);
    public TimeSpan CornerHoverDuration => TimeSpan.FromSeconds(CornerHoverSeconds);

    public ElasticBreathSettings Sanitize()
    {
        MinWorkMinutes = Math.Max(1, MinWorkMinutes);
        MaxWorkMinutes = Math.Max(MinWorkMinutes, MaxWorkMinutes);
        DefaultRestMinutes = Math.Max(1, DefaultRestMinutes);
        RestOvertimeMinutes = Math.Max(DefaultRestMinutes, RestOvertimeMinutes);
        MinEffectiveRestMinutes = Math.Clamp(MinEffectiveRestMinutes, 1, RestOvertimeMinutes);
        AwayThresholdMinutes = Math.Max(1, AwayThresholdMinutes);
        AutoRestAfterIdleSeconds = Math.Clamp(AutoRestAfterIdleSeconds, 10, 600);
        PostponeCooldownMinutes = Math.Max(1, PostponeCooldownMinutes);
        DailyPostponeLimit = Math.Max(0, DailyPostponeLimit);
        AutoTransitionCountdownSeconds = Math.Clamp(AutoTransitionCountdownSeconds, 1, 30);
        CornerHoverSeconds = Math.Clamp(CornerHoverSeconds, 0.5, 5);
        GlowMaxThicknessPixels = Math.Clamp(GlowMaxThicknessPixels, 12, 600);
        OverlayOpacity = Math.Clamp(OverlayOpacity, 0.1, 0.9);
        ReminderVolumePercent = Math.Clamp(ReminderVolumePercent, 0, 100);
        PreferredDisplay ??= "auto";
        Language = string.IsNullOrWhiteSpace(Language) ? "zh-CN" : Language;
        return this;
    }
}
