using System.Globalization;
using System.Windows;
using ElasticBreath.App.Domain;
using ElasticBreath.App.Services;
using Forms = System.Windows.Forms;

namespace ElasticBreath.App.UI;

public partial class SettingsWindow : Window
{
    private readonly ElasticBreathSettings _settings;
    private readonly LocalizationService _localization;

    public SettingsWindow(ElasticBreathSettings settings, LocalizationService localization)
    {
        InitializeComponent();
        _settings = settings;
        _localization = localization;
        ApplyLocalization();
        PopulateCombos();
        LoadValues();
    }

    private void ApplyLocalization()
    {
        Title = _localization.T("settings.title");
        TimeGroup.Header = _localization.T("settings.group.time");
        InteractionGroup.Header = _localization.T("settings.group.interaction");
        VisualGroup.Header = _localization.T("settings.group.visual");
        DisplayGroup.Header = _localization.T("settings.group.display");
        AudioGroup.Header = _localization.T("settings.group.audio");

        LanguageLabel.Text = _localization.T("settings.language");
        MinWorkLabel.Text = _localization.T("settings.min_work");
        MaxWorkLabel.Text = _localization.T("settings.max_work");
        DefaultRestLabel.Text = _localization.T("settings.default_rest");
        OvertimeRestLabel.Text = _localization.T("settings.overtime_rest");
        MinEffectiveRestLabel.Text = _localization.T("settings.min_effective_rest");
        AwayThresholdLabel.Text = _localization.T("settings.away_threshold");
        AutoRestIdleLabel.Text = _localization.T("settings.auto_rest_idle");
        PostponeCooldownLabel.Text = _localization.T("settings.postpone_cooldown");
        DailyPostponeLimitLabel.Text = _localization.T("settings.daily_postpone_limit");
        AutoCountdownLabel.Text = _localization.T("settings.auto_countdown");
        CornerHoverSecondsLabel.Text = _localization.T("settings.corner_hover_seconds");
        GlowMaxPxLabel.Text = _localization.T("settings.glow_max_px");
        OverlayOpacityLabel.Text = _localization.T("settings.overlay_opacity");
        PreferredDisplayLabel.Text = _localization.T("settings.preferred_display");
        VolumeLabel.Text = _localization.T("settings.volume");

        TopBarToggle.Content = _localization.T("settings.enable_top_bar");
        EdgeGlowToggle.Content = _localization.T("settings.enable_edge_glow");
        CornerHoverToggle.Content = _localization.T("settings.enable_corner_hover");
        FullscreenHideToggle.Content = _localization.T("settings.fullscreen_hide");
        SecondaryFlashToggle.Content = _localization.T("settings.secondary_flash");
        SoundToggle.Content = _localization.T("settings.enable_sound");
        FullscreenBeepToggle.Content = _localization.T("settings.fullscreen_beep");

        SaveButton.Content = _localization.T("settings.save");
        CancelButton.Content = _localization.T("settings.cancel");
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
    }

    private void LoadValues()
    {
        MinWorkBox.Text = _settings.MinWorkMinutes.ToString(CultureInfo.InvariantCulture);
        MaxWorkBox.Text = _settings.MaxWorkMinutes.ToString(CultureInfo.InvariantCulture);
        DefaultRestBox.Text = _settings.DefaultRestMinutes.ToString(CultureInfo.InvariantCulture);
        OvertimeRestBox.Text = _settings.RestOvertimeMinutes.ToString(CultureInfo.InvariantCulture);
        MinEffectiveRestBox.Text = _settings.MinEffectiveRestMinutes.ToString(CultureInfo.InvariantCulture);
        AwayThresholdBox.Text = _settings.AwayThresholdMinutes.ToString(CultureInfo.InvariantCulture);
        AutoRestIdleBox.Text = _settings.AutoRestAfterIdleSeconds.ToString(CultureInfo.InvariantCulture);
        PostponeCooldownBox.Text = _settings.PostponeCooldownMinutes.ToString(CultureInfo.InvariantCulture);
        DailyPostponeLimitBox.Text = _settings.DailyPostponeLimit.ToString(CultureInfo.InvariantCulture);
        AutoCountdownBox.Text = _settings.AutoTransitionCountdownSeconds.ToString(CultureInfo.InvariantCulture);
        CornerHoverSecondsBox.Text = _settings.CornerHoverSeconds.ToString(CultureInfo.InvariantCulture);
        GlowMaxPxBox.Text = _settings.GlowMaxThicknessPixels.ToString(CultureInfo.InvariantCulture);
        OverlayOpacityBox.Text = _settings.OverlayOpacity.ToString(CultureInfo.InvariantCulture);
        VolumeBox.Text = _settings.ReminderVolumePercent.ToString(CultureInfo.InvariantCulture);

        TopBarToggle.IsChecked = _settings.EnableTopProgressBar;
        EdgeGlowToggle.IsChecked = _settings.EnableEdgeGlow;
        CornerHoverToggle.IsChecked = _settings.EnableCornerHover;
        FullscreenHideToggle.IsChecked = _settings.FullscreenHideMode;
        SecondaryFlashToggle.IsChecked = _settings.EnableSecondaryMonitorFlash;
        SoundToggle.IsChecked = _settings.EnableSound;
        FullscreenBeepToggle.IsChecked = _settings.EnableFullscreenFallbackBeep;

        LanguageBox.SelectedItem = _settings.Language;
        PreferredDisplayBox.SelectedValue = string.IsNullOrWhiteSpace(_settings.PreferredDisplay) ? "auto" : _settings.PreferredDisplay;
        if (PreferredDisplayBox.SelectedIndex < 0)
        {
            PreferredDisplayBox.SelectedValue = "auto";
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyValues(out var error))
        {
            System.Windows.MessageBox.Show(error, _localization.T("dialog.invalid_settings_title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private bool TryApplyValues(out string error)
    {
        error = _localization.T("dialog.invalid_settings_body");

        var minWork = 0;
        var maxWork = 0;
        var defaultRest = 0;
        var overtimeRest = 0;
        var minEffectiveRest = 0;
        var awayThreshold = 0;
        var autoRestIdle = 0;
        var postponeCooldown = 0;
        var dailyPostpone = 0;
        var autoCountdown = 0;
        var cornerHover = 0d;
        var glowMaxPx = 0;
        var overlayOpacity = 0d;
        var volume = 0;

        var ok = int.TryParse(MinWorkBox.Text, out minWork)
            && int.TryParse(MaxWorkBox.Text, out maxWork)
            && int.TryParse(DefaultRestBox.Text, out defaultRest)
            && int.TryParse(OvertimeRestBox.Text, out overtimeRest)
            && int.TryParse(MinEffectiveRestBox.Text, out minEffectiveRest)
            && int.TryParse(AwayThresholdBox.Text, out awayThreshold)
            && int.TryParse(AutoRestIdleBox.Text, out autoRestIdle)
            && int.TryParse(PostponeCooldownBox.Text, out postponeCooldown)
            && int.TryParse(DailyPostponeLimitBox.Text, out dailyPostpone)
            && int.TryParse(AutoCountdownBox.Text, out autoCountdown)
            && double.TryParse(CornerHoverSecondsBox.Text, out cornerHover)
            && int.TryParse(GlowMaxPxBox.Text, out glowMaxPx)
            && double.TryParse(OverlayOpacityBox.Text, out overlayOpacity)
            && int.TryParse(VolumeBox.Text, out volume);

        if (!ok)
        {
            return false;
        }

        _settings.MinWorkMinutes = minWork;
        _settings.MaxWorkMinutes = maxWork;
        _settings.DefaultRestMinutes = defaultRest;
        _settings.RestOvertimeMinutes = overtimeRest;
        _settings.MinEffectiveRestMinutes = minEffectiveRest;
        _settings.AwayThresholdMinutes = awayThreshold;
        _settings.AutoRestAfterIdleSeconds = autoRestIdle;
        _settings.PostponeCooldownMinutes = postponeCooldown;
        _settings.DailyPostponeLimit = dailyPostpone;
        _settings.AutoTransitionCountdownSeconds = autoCountdown;
        _settings.CornerHoverSeconds = cornerHover;
        _settings.GlowMaxThicknessPixels = glowMaxPx;
        _settings.OverlayOpacity = overlayOpacity;
        _settings.ReminderVolumePercent = volume;

        _settings.EnableTopProgressBar = TopBarToggle.IsChecked == true;
        _settings.EnableEdgeGlow = EdgeGlowToggle.IsChecked == true;
        _settings.EnableCornerHover = CornerHoverToggle.IsChecked == true;
        _settings.FullscreenHideMode = FullscreenHideToggle.IsChecked == true;
        _settings.EnableSecondaryMonitorFlash = SecondaryFlashToggle.IsChecked == true;
        _settings.EnableSound = SoundToggle.IsChecked == true;
        _settings.EnableFullscreenFallbackBeep = FullscreenBeepToggle.IsChecked == true;

        _settings.Language = LanguageBox.SelectedItem?.ToString() ?? "zh-CN";
        _settings.PreferredDisplay = (PreferredDisplayBox.SelectedValue?.ToString() ?? "auto").Trim();

        _settings.Sanitize();
        return true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed record DisplayItem(string Value, string Label);
}
