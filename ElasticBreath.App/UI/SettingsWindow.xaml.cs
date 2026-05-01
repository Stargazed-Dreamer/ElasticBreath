using System.Globalization;
using System.Windows;
using Controls = System.Windows.Controls;
using ElasticBreath.App.Domain;
using ElasticBreath.App.Services;
using Forms = System.Windows.Forms;

namespace ElasticBreath.App.UI;

public partial class SettingsWindow : Window
{
    private readonly ElasticBreathSettings _settings;
    private readonly LocalizationService _localization;
    private readonly Dictionary<Controls.TextBox, Controls.TextBlock> _errors = new();
    private bool _isLoading;

    public SettingsWindow(ElasticBreathSettings settings, LocalizationService localization)
    {
        InitializeComponent();
        _settings = settings;
        _localization = localization;
        BindErrorBlocks();
        ApplyLocalization();
        PopulateCombos();
        LoadValues();
        ValidateAll();
    }

    public event EventHandler? SettingsApplied;

    private void BindErrorBlocks()
    {
        _errors[MinWorkBox] = MinWorkError;
        _errors[MaxWorkBox] = MaxWorkError;
        _errors[DefaultRestBox] = DefaultRestError;
        _errors[OvertimeRestBox] = OvertimeRestError;
        _errors[MinEffectiveRestBox] = MinEffectiveRestError;
        _errors[AwayThresholdBox] = AwayThresholdError;
        _errors[AutoRestIdleBox] = AutoRestIdleError;
        _errors[IdleToWorkDetectBox] = IdleToWorkDetectError;
        _errors[RestToWorkDetectBox] = RestToWorkDetectError;
        _errors[SmartDetectGapBox] = SmartDetectGapError;
        _errors[AutoCountdownBox] = AutoCountdownError;
        _errors[CornerHoverSecondsBox] = CornerHoverSecondsError;
        _errors[GlowMaxPxBox] = GlowMaxPxError;
        _errors[OverlayOpacityBox] = OverlayOpacityError;
        _errors[VolumeBox] = VolumeError;
    }

    private void ApplyLocalization()
    {
        Title = _localization.T("settings.title");
        TimeGroup.Header = _localization.T("settings.group.time");
        InteractionGroup.Header = _localization.T("settings.group.interaction");
        VisualGroup.Header = _localization.T("settings.group.visual");
        DisplayGroup.Header = _localization.T("settings.group.display");
        AudioGroup.Header = _localization.T("settings.group.audio");

        MinWorkLabel.Text = LabeledRange("settings.min_work", ElasticBreathSettings.MinWorkSecondsMin, ElasticBreathSettings.MinWorkSecondsMax);
        MaxWorkLabel.Text = LabeledRange("settings.max_work", ElasticBreathSettings.MaxWorkSecondsMin, ElasticBreathSettings.MaxWorkSecondsMax);
        DefaultRestLabel.Text = LabeledRange("settings.default_rest", ElasticBreathSettings.DefaultRestSecondsMin, ElasticBreathSettings.DefaultRestSecondsMax);
        OvertimeRestLabel.Text = LabeledRange("settings.overtime_rest", ElasticBreathSettings.RestOvertimeSecondsMin, ElasticBreathSettings.RestOvertimeSecondsMax);
        MinEffectiveRestLabel.Text = LabeledRange("settings.min_effective_rest", ElasticBreathSettings.MinEffectiveRestSecondsMin, ElasticBreathSettings.MinEffectiveRestSecondsMax);
        AwayThresholdLabel.Text = LabeledRange("settings.away_threshold", ElasticBreathSettings.AwayThresholdSecondsMin, ElasticBreathSettings.AwayThresholdSecondsMax);
        AutoRestIdleLabel.Text = LabeledRange("settings.auto_rest_idle", ElasticBreathSettings.AutoRestAfterIdleSecondsMin, ElasticBreathSettings.AutoRestAfterIdleSecondsMax);
        IdleToWorkDetectLabel.Text = LabeledRange("settings.idle_to_work_detect", ElasticBreathSettings.IdleToWorkDetectSecondsMin, ElasticBreathSettings.IdleToWorkDetectSecondsMax);
        RestToWorkDetectLabel.Text = LabeledRange("settings.rest_to_work_detect", ElasticBreathSettings.RestToWorkDetectSecondsMin, ElasticBreathSettings.RestToWorkDetectSecondsMax);
        SmartDetectGapLabel.Text = LabeledRange("settings.smart_detect_gap", ElasticBreathSettings.SmartDetectGapSecondsMin, ElasticBreathSettings.SmartDetectGapSecondsMax);
        AutoCountdownLabel.Text = LabeledRange("settings.auto_countdown", ElasticBreathSettings.AutoTransitionCountdownSecondsMin, ElasticBreathSettings.AutoTransitionCountdownSecondsMax);
        CornerHoverSecondsLabel.Text = _localization.Tf("settings.label_with_range_float", _localization.T("settings.corner_hover_seconds"), ElasticBreathSettings.CornerHoverSecondsMin, ElasticBreathSettings.CornerHoverSecondsMax);
        GlowMaxPxLabel.Text = _localization.T("settings.glow_max_px");
        OverlayOpacityLabel.Text = _localization.T("settings.overlay_opacity");
        PreferredDisplayLabel.Text = _localization.T("settings.preferred_display");
        LanguageLabel.Text = _localization.T("settings.language");
        VolumeLabel.Text = _localization.T("settings.volume");
        CloseBehaviorLabel.Text = _localization.T("settings.close_behavior");

        TopBarToggle.Content = _localization.T("settings.enable_top_bar");
        EdgeGlowToggle.Content = _localization.T("settings.enable_edge_glow");
        CornerHoverToggle.Content = _localization.T("settings.enable_corner_hover");
        FullscreenHideToggle.Content = _localization.T("settings.fullscreen_hide");
        SecondaryFlashToggle.Content = _localization.T("settings.secondary_flash");
        SoundToggle.Content = _localization.T("settings.enable_sound");
        FullscreenBeepToggle.Content = _localization.T("settings.fullscreen_beep");

        ApplyButton.Content = _localization.T("settings.apply");
        SaveButton.Content = _localization.T("settings.save");
        CancelButton.Content = _localization.T("settings.cancel");
    }

    private string LabeledRange(string labelKey, int min, int max)
        => _localization.Tf("settings.label_with_range", _localization.T(labelKey), min, max);

    /* 优先使用用户保存的原始表达式（如 "35*60"），否则回退到数值的字符串形式 */
    private string GetRawOrValue(string key, double value)
    {
        if (_settings.RawExpressions.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private void PopulateCombos()
    {
        LanguageBox.ItemsSource = _localization.AvailableLanguages;
        if (LanguageBox.Items.Count == 0)
        {
            LanguageBox.Items.Add("zh-CN");
            LanguageBox.Items.Add("en-US");
        }

        PreferredDisplayBox.Items.Clear();
        PreferredDisplayBox.Items.Add(new DisplayItem("auto", _localization.T("settings.display.auto")));
        foreach (var screen in Forms.Screen.AllScreens)
        {
            PreferredDisplayBox.Items.Add(new DisplayItem(screen.DeviceName, $"{screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})"));
        }
        PreferredDisplayBox.DisplayMemberPath = nameof(DisplayItem.Label);
        PreferredDisplayBox.SelectedValuePath = nameof(DisplayItem.Value);

        CloseBehaviorBox.Items.Clear();
        CloseBehaviorBox.Items.Add(new CloseBehaviorItem(CloseBehavior.Exit, _localization.T("settings.close_behavior.exit")));
        CloseBehaviorBox.Items.Add(new CloseBehaviorItem(CloseBehavior.MinimizeToTray, _localization.T("settings.close_behavior.tray")));
        CloseBehaviorBox.DisplayMemberPath = nameof(CloseBehaviorItem.Label);
        CloseBehaviorBox.SelectedValuePath = nameof(CloseBehaviorItem.Value);
    }

    private void LoadValues()
    {
        _isLoading = true;
        MinWorkBox.Text = GetRawOrValue("minWorkSeconds", _settings.MinWorkSeconds);
        MaxWorkBox.Text = GetRawOrValue("maxWorkSeconds", _settings.MaxWorkSeconds);
        DefaultRestBox.Text = GetRawOrValue("defaultRestSeconds", _settings.DefaultRestSeconds);
        OvertimeRestBox.Text = GetRawOrValue("restOvertimeSeconds", _settings.RestOvertimeSeconds);
        MinEffectiveRestBox.Text = GetRawOrValue("minEffectiveRestSeconds", _settings.MinEffectiveRestSeconds);
        AwayThresholdBox.Text = GetRawOrValue("awayThresholdSeconds", _settings.AwayThresholdSeconds);
        AutoRestIdleBox.Text = GetRawOrValue("autoRestAfterIdleSeconds", _settings.AutoRestAfterIdleSeconds);
        IdleToWorkDetectBox.Text = GetRawOrValue("idleToWorkDetectSeconds", _settings.IdleToWorkDetectSeconds);
        RestToWorkDetectBox.Text = GetRawOrValue("restToWorkDetectSeconds", _settings.RestToWorkDetectSeconds);
        SmartDetectGapBox.Text = GetRawOrValue("smartDetectGapSeconds", _settings.SmartDetectGapSeconds);
        AutoCountdownBox.Text = GetRawOrValue("autoTransitionCountdownSeconds", _settings.AutoTransitionCountdownSeconds);
        CornerHoverSecondsBox.Text = GetRawOrValue("cornerHoverSeconds", _settings.CornerHoverSeconds);
        GlowMaxPxBox.Text = GetRawOrValue("glowMaxThicknessPixels", _settings.GlowMaxThicknessPixels);
        OverlayOpacityBox.Text = GetRawOrValue("overlayOpacity", _settings.OverlayOpacity);
        VolumeBox.Text = GetRawOrValue("reminderVolumePercent", _settings.ReminderVolumePercent);

        TopBarToggle.IsChecked = _settings.EnableTopProgressBar;
        EdgeGlowToggle.IsChecked = _settings.EnableEdgeGlow;
        CornerHoverToggle.IsChecked = _settings.EnableCornerHover;
        FullscreenHideToggle.IsChecked = _settings.FullscreenHideMode;
        SecondaryFlashToggle.IsChecked = _settings.EnableSecondaryMonitorFlash;
        SoundToggle.IsChecked = _settings.EnableSound;
        FullscreenBeepToggle.IsChecked = _settings.EnableFullscreenFallbackBeep;

        LanguageBox.SelectedItem = _settings.Language;
        PreferredDisplayBox.SelectedValue = string.IsNullOrWhiteSpace(_settings.PreferredDisplay) ? "auto" : _settings.PreferredDisplay;
        CloseBehaviorBox.SelectedValue = _settings.CloseBehaviorOnMainWindowClose;
        _isLoading = false;
    }

    private void AnyInputChanged(object sender, Controls.TextChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }
        ValidateAll();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyValues(showMessage: true))
        {
            return;
        }

        SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyValues(showMessage: true))
        {
            return;
        }

        SettingsApplied?.Invoke(this, EventArgs.Empty);
        DialogResult = true;
        Close();
    }

    private bool TryApplyValues(bool showMessage)
    {
        if (!ValidateAll())
        {
            if (showMessage)
            {
                System.Windows.MessageBox.Show(_localization.T("dialog.invalid_settings_body"), _localization.T("dialog.invalid_settings_title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return false;
        }

        _settings.MinWorkSeconds = ReadInt(MinWorkBox);
        _settings.MaxWorkSeconds = ReadInt(MaxWorkBox);
        _settings.DefaultRestSeconds = ReadInt(DefaultRestBox);
        _settings.RestOvertimeSeconds = ReadInt(OvertimeRestBox);
        _settings.MinEffectiveRestSeconds = ReadInt(MinEffectiveRestBox);
        _settings.AwayThresholdSeconds = ReadInt(AwayThresholdBox);
        _settings.AutoRestAfterIdleSeconds = ReadInt(AutoRestIdleBox);
        _settings.IdleToWorkDetectSeconds = ReadInt(IdleToWorkDetectBox);
        _settings.RestToWorkDetectSeconds = ReadInt(RestToWorkDetectBox);
        _settings.SmartDetectGapSeconds = ReadInt(SmartDetectGapBox);
        _settings.AutoTransitionCountdownSeconds = ReadInt(AutoCountdownBox);
        _settings.CornerHoverSeconds = ReadDouble(CornerHoverSecondsBox);
        _settings.GlowMaxThicknessPixels = ReadInt(GlowMaxPxBox);
        _settings.OverlayOpacity = ReadDouble(OverlayOpacityBox);
        _settings.ReminderVolumePercent = ReadInt(VolumeBox);

        /* 保存用户输入的原始表达式文本，以便写入 JSON 时保留可读性 */
        _settings.RawExpressions["minWorkSeconds"] = MinWorkBox.Text.Trim();
        _settings.RawExpressions["maxWorkSeconds"] = MaxWorkBox.Text.Trim();
        _settings.RawExpressions["defaultRestSeconds"] = DefaultRestBox.Text.Trim();
        _settings.RawExpressions["restOvertimeSeconds"] = OvertimeRestBox.Text.Trim();
        _settings.RawExpressions["minEffectiveRestSeconds"] = MinEffectiveRestBox.Text.Trim();
        _settings.RawExpressions["awayThresholdSeconds"] = AwayThresholdBox.Text.Trim();
        _settings.RawExpressions["autoRestAfterIdleSeconds"] = AutoRestIdleBox.Text.Trim();
        _settings.RawExpressions["idleToWorkDetectSeconds"] = IdleToWorkDetectBox.Text.Trim();
        _settings.RawExpressions["restToWorkDetectSeconds"] = RestToWorkDetectBox.Text.Trim();
        _settings.RawExpressions["smartDetectGapSeconds"] = SmartDetectGapBox.Text.Trim();
        _settings.RawExpressions["autoTransitionCountdownSeconds"] = AutoCountdownBox.Text.Trim();
        _settings.RawExpressions["cornerHoverSeconds"] = CornerHoverSecondsBox.Text.Trim();
        _settings.RawExpressions["glowMaxThicknessPixels"] = GlowMaxPxBox.Text.Trim();
        _settings.RawExpressions["overlayOpacity"] = OverlayOpacityBox.Text.Trim();
        _settings.RawExpressions["reminderVolumePercent"] = VolumeBox.Text.Trim();
        _settings.Language = LanguageBox.SelectedItem?.ToString() ?? "zh-CN";
        _settings.PreferredDisplay = (PreferredDisplayBox.SelectedValue?.ToString() ?? "auto").Trim();
        _settings.CloseBehaviorOnMainWindowClose = CloseBehaviorBox.SelectedValue is CloseBehavior b ? b : CloseBehavior.MinimizeToTray;

        _settings.EnableTopProgressBar = TopBarToggle.IsChecked == true;
        _settings.EnableEdgeGlow = EdgeGlowToggle.IsChecked == true;
        _settings.EnableCornerHover = CornerHoverToggle.IsChecked == true;
        _settings.FullscreenHideMode = FullscreenHideToggle.IsChecked == true;
        _settings.EnableSecondaryMonitorFlash = SecondaryFlashToggle.IsChecked == true;
        _settings.EnableSound = SoundToggle.IsChecked == true;
        _settings.EnableFullscreenFallbackBeep = FullscreenBeepToggle.IsChecked == true;
        _settings.Sanitize();
        return true;
    }

    private bool ValidateAll()
    {
        var ok = true;
        ClearErrors();

        ok &= ValidateInt(MinWorkBox, ElasticBreathSettings.MinWorkSecondsMin, ElasticBreathSettings.MinWorkSecondsMax);
        ok &= ValidateInt(MaxWorkBox, ElasticBreathSettings.MaxWorkSecondsMin, ElasticBreathSettings.MaxWorkSecondsMax);
        ok &= ValidateInt(DefaultRestBox, ElasticBreathSettings.DefaultRestSecondsMin, ElasticBreathSettings.DefaultRestSecondsMax);
        ok &= ValidateInt(OvertimeRestBox, ElasticBreathSettings.RestOvertimeSecondsMin, ElasticBreathSettings.RestOvertimeSecondsMax);
        ok &= ValidateInt(MinEffectiveRestBox, ElasticBreathSettings.MinEffectiveRestSecondsMin, ElasticBreathSettings.MinEffectiveRestSecondsMax);
        ok &= ValidateInt(AwayThresholdBox, ElasticBreathSettings.AwayThresholdSecondsMin, ElasticBreathSettings.AwayThresholdSecondsMax);
        ok &= ValidateInt(AutoRestIdleBox, ElasticBreathSettings.AutoRestAfterIdleSecondsMin, ElasticBreathSettings.AutoRestAfterIdleSecondsMax);
        ok &= ValidateInt(IdleToWorkDetectBox, ElasticBreathSettings.IdleToWorkDetectSecondsMin, ElasticBreathSettings.IdleToWorkDetectSecondsMax);
        ok &= ValidateInt(RestToWorkDetectBox, ElasticBreathSettings.RestToWorkDetectSecondsMin, ElasticBreathSettings.RestToWorkDetectSecondsMax);
        ok &= ValidateInt(SmartDetectGapBox, ElasticBreathSettings.SmartDetectGapSecondsMin, ElasticBreathSettings.SmartDetectGapSecondsMax);
        ok &= ValidateInt(AutoCountdownBox, ElasticBreathSettings.AutoTransitionCountdownSecondsMin, ElasticBreathSettings.AutoTransitionCountdownSecondsMax);
        ok &= ValidateDouble(CornerHoverSecondsBox, ElasticBreathSettings.CornerHoverSecondsMin, ElasticBreathSettings.CornerHoverSecondsMax);
        ok &= ValidateInt(GlowMaxPxBox, 12, 600);
        ok &= ValidateDouble(OverlayOpacityBox, 0.1, 0.9);
        ok &= ValidateInt(VolumeBox, 0, 100);

        if (ok)
        {
            var minWork = ReadInt(MinWorkBox);
            var maxWork = ReadInt(MaxWorkBox);
            var rest = ReadInt(DefaultRestBox);
            var restOvertime = ReadInt(OvertimeRestBox);
            var minRest = ReadInt(MinEffectiveRestBox);
            var awayThreshold = ReadInt(AwayThresholdBox);

            if (maxWork < minWork)
            {
                SetError(MaxWorkBox, _localization.T("settings.err.max_ge_min_work"));
                ok = false;
            }
            if (restOvertime < rest)
            {
                SetError(OvertimeRestBox, _localization.T("settings.err.overtime_ge_default_rest"));
                ok = false;
            }
            if (minRest > restOvertime)
            {
                SetError(MinEffectiveRestBox, _localization.T("settings.err.min_effective_le_overtime"));
                ok = false;
            }
            if (awayThreshold <= restOvertime)
            {
                SetError(AwayThresholdBox, _localization.T("settings.err.away_gt_overtime"));
                ok = false;
            }
        }

        ApplyButton.IsEnabled = ok;
        SaveButton.IsEnabled = ok;
        return ok;
    }

    private bool ValidateInt(Controls.TextBox box, int min, int max)
    {
        if (!ExpressionEvaluator.TryEvaluate(box.Text, out var value, out _))
        {
            SetError(box, _localization.T("settings.err.expr"));
            return false;
        }

        if (Math.Abs(value - Math.Round(value)) > 1e-9)
        {
            SetError(box, _localization.T("settings.err.integer"));
            return false;
        }

        var intValue = (int)Math.Round(value);
        if (intValue < min || intValue > max)
        {
            SetError(box, _localization.Tf("settings.err.range", min, max));
            return false;
        }

        return true;
    }

    private bool ValidateDouble(Controls.TextBox box, double min, double max)
    {
        if (!ExpressionEvaluator.TryEvaluate(box.Text, out var value, out _))
        {
            SetError(box, _localization.T("settings.err.expr"));
            return false;
        }

        if (value < min || value > max)
        {
            SetError(box, _localization.Tf("settings.err.range_float", min, max));
            return false;
        }

        return true;
    }

    private int ReadInt(Controls.TextBox box)
    {
        _ = ExpressionEvaluator.TryEvaluate(box.Text, out var value, out _);
        return (int)Math.Round(value);
    }

    private double ReadDouble(Controls.TextBox box)
    {
        _ = ExpressionEvaluator.TryEvaluate(box.Text, out var value, out _);
        return value;
    }

    private void ClearErrors()
    {
        foreach (var entry in _errors.Values)
        {
            entry.Text = string.Empty;
        }
    }

    private void SetError(Controls.TextBox box, string message)
    {
        if (_errors.TryGetValue(box, out var block))
        {
            block.Text = message;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed record DisplayItem(string Value, string Label);
    private sealed record CloseBehaviorItem(CloseBehavior Value, string Label);
}
