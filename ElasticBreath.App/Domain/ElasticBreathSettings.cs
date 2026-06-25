namespace ElasticBreath.App.Domain;

/// <summary>
/// 定义应用程序关闭行为的枚举。
/// </summary>
public enum CloseBehavior
{
    // 退出应用程序
    Exit = 0,
    // 最小化到系统托盘
    MinimizeToTray = 1
}

/// <summary>
/// 弹性呼吸设置类，用于存储和管理应用程序的配置参数，包括工作时间、休息时间、检测间隔等。
/// </summary>
public sealed class ElasticBreathSettings
{
    // 最小工作秒数的最小值和最大值
    public const int MinWorkSecondsMin = 1;
    public const int MinWorkSecondsMax = 7200;
    // 最大工作秒数的最小值和最大值
    public const int MaxWorkSecondsMin = 1;
    public const int MaxWorkSecondsMax = 14400;
    // 默认休息秒数的最小值和最大值
    public const int DefaultRestSecondsMin = 1;
    public const int DefaultRestSecondsMax = 7200;
    // 休息加班秒数的最小值和最大值
    public const int RestOvertimeSecondsMin = 1;
    public const int RestOvertimeSecondsMax = 14400;
    // 最小有效休息秒数的最小值和最大值
    public const int MinEffectiveRestSecondsMin = 1;
    public const int MinEffectiveRestSecondsMax = 7200;
    // 离开阈值秒数的最小值和最大值
    public const int AwayThresholdSecondsMin = 1;
    public const int AwayThresholdSecondsMax = 7200;
    // 空闲后自动休息秒数的最小值和最大值
    public const int AutoRestAfterIdleSecondsMin = 10;
    public const int AutoRestAfterIdleSecondsMax = 3600;
    // 空闲到工作检测秒数的最小值和最大值
    public const int IdleToWorkDetectSecondsMin = 1;
    public const int IdleToWorkDetectSecondsMax = 600;
    // 休息到工作检测秒数的最小值和最大值
    public const int RestToWorkDetectSecondsMin = 1;
    public const int RestToWorkDetectSecondsMax = 600;
    // 智能检测间隔秒数的最小值和最大值
    public const int SmartDetectGapSecondsMin = 1;
    public const int SmartDetectGapSecondsMax = 15;
    // 自动过渡倒计时秒数的最小值和最大值
    public const int AutoTransitionCountdownSecondsMin = 1;
    public const int AutoTransitionCountdownSecondsMax = 120;
    // 角落悬停秒数的最小值和最大值
    public const double CornerHoverSecondsMin = 0.5;
    public const double CornerHoverSecondsMax = 10;

    // 语言设置，默认为中文（中国）
    public string Language { get; set; } = "zh-CN";
    // 主窗口关闭时的行为，默认最小化到系统托盘
    public CloseBehavior CloseBehaviorOnMainWindowClose { get; set; } = CloseBehavior.MinimizeToTray;

    // 最小工作秒数，默认35分钟（以秒为单位）
    public int MinWorkSeconds { get; set; } = 35 * 60;
    // 最大工作秒数，默认45分钟（以秒为单位）
    public int MaxWorkSeconds { get; set; } = 45 * 60;
    // 默认休息秒数，默认5分钟（以秒为单位）
    public int DefaultRestSeconds { get; set; } = 5 * 60;
    // 休息加班秒数，默认8分钟（以秒为单位）
    public int RestOvertimeSeconds { get; set; } = 8 * 60;
    // 最小有效休息秒数，默认3分钟（以秒为单位）
    public int MinEffectiveRestSeconds { get; set; } = 3 * 60;
    // 离开阈值秒数，默认3分钟（以秒为单位）
    public int AwayThresholdSeconds { get; set; } = 3 * 60;
    // 空闲后自动休息秒数，默认30秒
    public int AutoRestAfterIdleSeconds { get; set; } = 30;
    // 空闲到工作检测秒数，默认4秒
    public int IdleToWorkDetectSeconds { get; set; } = 4;
    // 休息到工作检测秒数，默认30秒
    public int RestToWorkDetectSeconds { get; set; } = 30;
    // 智能检测间隔秒数，默认2秒
    public int SmartDetectGapSeconds { get; set; } = 2;
    // 自动过渡倒计时秒数，默认5秒
    public int AutoTransitionCountdownSeconds { get; set; } = 5;
    // 角落悬停秒数，默认1.5秒
    public double CornerHoverSeconds { get; set; } = 1.5;

    // 是否启用顶部进度条
    public bool EnableTopProgressBar { get; set; }
    // 是否启用边缘发光效果，默认为true
    public bool EnableEdgeGlow { get; set; } = true;
    // 是否启用角落悬停功能，默认为true
    public bool EnableCornerHover { get; set; } = true;
    // 发光最大厚度像素，默认80像素
    public int GlowMaxThicknessPixels { get; set; } = 80;
    // 覆盖层不透明度，默认0.35
    public double OverlayOpacity { get; set; } = 0.35;
    // 全屏隐藏模式
    public bool FullscreenHideMode { get; set; }
    // 是否启用辅助显示器闪烁，默认为true
    public bool EnableSecondaryMonitorFlash { get; set; } = true;

    /* 周期性重新置顶，防止 Win+D 等操作将悬浮层推到后面 */
    // 是否启用周期性重新置顶
    public bool EnablePeriodicReTopmost { get; set; }
    // 重新置顶间隔秒数，默认5秒
    public int ReTopmostIntervalSeconds { get; set; } = 5;
    // 重新置顶间隔秒数的最小值和最大值
    public const int ReTopmostIntervalSecondsMin = 1;
    public const int ReTopmostIntervalSecondsMax = 60;

    // 是否启用声音提醒
    public bool EnableSound { get; set; }
    // 提醒音量百分比，默认50%
    public int ReminderVolumePercent { get; set; } = 50;
    // 是否启用全屏回退蜂鸣声
    public bool EnableFullscreenFallbackBeep { get; set; }

    // 首选显示器设置，默认为自动选择
    public string PreferredDisplay { get; set; } = "auto";

    /* 用户在设置界面输入的原始表达式文本（如 "35*60"），保存到 JSON 时使用原始文本以方便阅读 */
    public Dictionary<string, string> RawExpressions { get; set; } = new();

    // 以下是属性对应的TimeSpan计算属性，方便使用TimeSpan类型访问
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

    /// <summary>
    /// 清理和验证设置值，确保所有属性在有效范围内，并处理依赖关系。
    /// </summary>
    /// <returns>返回清理后的当前实例。</returns>
    public ElasticBreathSettings Sanitize()
    {
        // 确保最小工作秒数在有效范围内
        MinWorkSeconds = Math.Clamp(MinWorkSeconds, MinWorkSecondsMin, MinWorkSecondsMax);
        // 确保最大工作秒数至少为最小工作秒数，并在有效范围内
        MaxWorkSeconds = Math.Clamp(MaxWorkSeconds, Math.Max(MinWorkSeconds, MaxWorkSecondsMin), MaxWorkSecondsMax);
        // 确保默认休息秒数在有效范围内
        DefaultRestSeconds = Math.Clamp(DefaultRestSeconds, DefaultRestSecondsMin, DefaultRestSecondsMax);
        // 确保休息加班秒数至少为默认休息秒数，并在有效范围内
        RestOvertimeSeconds = Math.Clamp(RestOvertimeSeconds, Math.Max(DefaultRestSeconds, RestOvertimeSecondsMin), RestOvertimeSecondsMax);
        // 确保最小有效休息秒数不超过休息加班秒数，并在有效范围内
        MinEffectiveRestSeconds = Math.Clamp(MinEffectiveRestSeconds, MinEffectiveRestSecondsMin, Math.Min(MinEffectiveRestSecondsMax, RestOvertimeSeconds));
        // 确保离开阈值秒数大于休息加班秒数，并在有效范围内
        AwayThresholdSeconds = Math.Clamp(AwayThresholdSeconds, Math.Max(RestOvertimeSeconds + 1, AwayThresholdSecondsMin), AwayThresholdSecondsMax);
        // 确保空闲后自动休息秒数在有效范围内
        AutoRestAfterIdleSeconds = Math.Clamp(AutoRestAfterIdleSeconds, AutoRestAfterIdleSecondsMin, AutoRestAfterIdleSecondsMax);
        // 确保空闲到工作检测秒数在有效范围内
        IdleToWorkDetectSeconds = Math.Clamp(IdleToWorkDetectSeconds, IdleToWorkDetectSecondsMin, IdleToWorkDetectSecondsMax);
        // 确保休息到工作检测秒数在有效范围内
        RestToWorkDetectSeconds = Math.Clamp(RestToWorkDetectSeconds, RestToWorkDetectSecondsMin, RestToWorkDetectSecondsMax);
        // 确保智能检测间隔秒数在有效范围内
        SmartDetectGapSeconds = Math.Clamp(SmartDetectGapSeconds, SmartDetectGapSecondsMin, SmartDetectGapSecondsMax);
        // 确保自动过渡倒计时秒数在有效范围内
        AutoTransitionCountdownSeconds = Math.Clamp(AutoTransitionCountdownSeconds, AutoTransitionCountdownSecondsMin, AutoTransitionCountdownSecondsMax);
        // 确保角落悬停秒数在有效范围内
        CornerHoverSeconds = Math.Clamp(CornerHoverSeconds, CornerHoverSecondsMin, CornerHoverSecondsMax);
        // 确保发光最大厚度像素在12到600之间
        GlowMaxThicknessPixels = Math.Clamp(GlowMaxThicknessPixels, 12, 600);
        // 确保覆盖层不透明度在0.1到0.9之间
        OverlayOpacity = Math.Clamp(OverlayOpacity, 0.1, 0.9);
        // 确保提醒音量百分比在0到100之间
        ReminderVolumePercent = Math.Clamp(ReminderVolumePercent, 0, 100);
        // 确保重新置顶间隔秒数在有效范围内
        ReTopmostIntervalSeconds = Math.Clamp(ReTopmostIntervalSeconds, ReTopmostIntervalSecondsMin, ReTopmostIntervalSecondsMax);
        // 如果首选显示器设置为空，则默认设为自动选择
        PreferredDisplay ??= "auto";
        // 如果语言设置为空或空白，则默认设为中文（中国）
        Language = string.IsNullOrWhiteSpace(Language) ? "zh-CN" : Language;
        return this;
    }
}
