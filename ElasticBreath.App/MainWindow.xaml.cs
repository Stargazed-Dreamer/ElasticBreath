using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ElasticBreath.App.Domain;
using ElasticBreath.App.Services;
using ElasticBreath.App.UI;
using Drawing = System.Drawing;
using MediaColor = System.Windows.Media.Color;
using Forms = System.Windows.Forms;

namespace ElasticBreath.App;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore;
    private readonly ElasticBreathSettings _settings;
    private readonly BreathEngine _engine;
    private readonly CornerTriggerService _cornerTrigger;
    private readonly DisplayTargetService _displayTargetService;
    private readonly EdgeOverlayWindow _overlayWindow;
    private readonly CountdownNotificationWindow _notificationWindow;
    private readonly DispatcherTimer _cornerPollTimer;

    private Forms.NotifyIcon? _trayIcon;
    private bool _exitRequested;
    private IntPtr _mainWindowHandle;

    public MainWindow()
    {
        InitializeComponent();

        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
        _engine = new BreathEngine(_settings, new InputMonitor());
        _cornerTrigger = new CornerTriggerService();
        _displayTargetService = new DisplayTargetService();
        _overlayWindow = new EdgeOverlayWindow();
        _notificationWindow = new CountdownNotificationWindow();
        _cornerPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        _engine.SnapshotChanged += OnSnapshotChanged;
        _notificationWindow.CancelRequested += (_, _) => _engine.CancelPendingTransition();
        _cornerPollTimer.Tick += OnCornerPollTimerTick;

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        StateChanged += OnStateChanged;

        ApplySettingsToControls();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _mainWindowHandle = new WindowInteropHelper(this).Handle;
        InitializeTrayIcon();
        _engine.Start();
        _cornerPollTimer.Start();
        RenderSnapshot(_engine.Snapshot);
        UpdateOverlayAndNotifications(_engine.Snapshot);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        _trayIcon?.ShowBalloonTip(1500, "ElasticBreath", "App minimized to tray.", Forms.ToolTipIcon.Info);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _cornerPollTimer.Stop();
        _engine.Dispose();
        _overlayWindow.Close();
        _notificationWindow.Close();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }

    private void InitializeTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Exit", null, (_, _) => ExitFromTray());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Visible = true,
            Text = "ElasticBreath",
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        _exitRequested = true;
        Close();
    }

    private void OnSnapshotChanged(object? sender, EngineSnapshot snapshot)
    {
        RenderSnapshot(snapshot);
        UpdateOverlayAndNotifications(snapshot);
    }

    private void OnCornerPollTimerTick(object? sender, EventArgs e)
    {
        if (!_settings.EnableCornerHover)
        {
            return;
        }

        var snapshot = _engine.Snapshot;
        if (snapshot.State is not (ElasticBreathState.Working or ElasticBreathState.Resting))
        {
            return;
        }

        var targetScreen = _displayTargetService.GetTargetScreen(_settings.PreferredDisplay);
        var bounds = DisplayTargetService.ToWpfRect(targetScreen.Bounds);
        var cursor = Forms.Cursor.Position;
        if (_cornerTrigger.TryTrigger(bounds, new System.Windows.Point(cursor.X, cursor.Y), _settings.CornerHoverDuration))
        {
            _engine.TriggerCornerTransition();
        }
    }

    private void RenderSnapshot(EngineSnapshot snapshot)
    {
        StateText.Text = $"State: {snapshot.State}";
        CenterStateText.Text = snapshot.State.ToString().ToUpperInvariant();
        TodayWorkText.Text = $"Working total: {FormatDuration(snapshot.TotalWorkingToday)}";
        TodayRestText.Text = $"Resting total: {FormatDuration(snapshot.TotalRestingToday)}";
        PauseRemindersButton.Content = snapshot.RemindersPaused ? "Resume Reminders" : "Pause Reminders";

        if (snapshot.PendingTransition is null)
        {
            PendingText.Text = "Pending transition: none";
        }
        else
        {
            var seconds = Math.Max(0, (int)Math.Ceiling(snapshot.PendingTransition.Remaining.TotalSeconds));
            PendingText.Text = $"Pending transition: {snapshot.PendingTransition.Kind} ({seconds}s)";
        }

        var elapsed = snapshot.State switch
        {
            ElasticBreathState.Resting => snapshot.RestingCycleElapsed,
            _ => snapshot.WorkingCycleElapsed
        };
        TimerText.Text = FormatDuration(elapsed, shortStyle: true);

        var pressureText = snapshot.State switch
        {
            ElasticBreathState.Working => $"Pressure: {snapshot.WorkingPressure}",
            ElasticBreathState.Resting => $"Pressure: {snapshot.RestPressure}",
            ElasticBreathState.Paused => "Pressure: Paused",
            _ => "Pressure: Idle"
        };
        PressureText.Text = pressureText;

        var progress = snapshot.State switch
        {
            ElasticBreathState.Working => snapshot.WorkingProgressRatio(_settings.MaxWorkThreshold),
            ElasticBreathState.Resting => snapshot.RestingProgressRatio(_settings.RestOvertimeThreshold),
            _ => 0
        };
        var color = ResolveProgressColor(snapshot);
        ProgressArc.Stroke = new SolidColorBrush(color);
        ProgressArc.Data = BuildArcGeometry(new System.Windows.Point(140, 140), 106, progress);
    }

    private void UpdateOverlayAndNotifications(EngineSnapshot snapshot)
    {
        var targetScreen = _displayTargetService.GetTargetScreen(_settings.PreferredDisplay);
        MonitorText.Text = $"Target display: {targetScreen.DeviceName}";
        var screenBounds = DisplayTargetService.ToWpfRect(targetScreen.Bounds);

        _overlayWindow.SetBounds(screenBounds);
        _notificationWindow.PositionAtTopRight(screenBounds);

        var hideForFullscreen = _settings.FullscreenHideMode && _displayTargetService.IsFullscreenForeground(targetScreen, _mainWindowHandle);
        var overlayState = ResolveOverlayState(snapshot);
        var topProgressRatio = snapshot.State switch
        {
            ElasticBreathState.Working => snapshot.WorkingProgressRatio(_settings.MaxWorkThreshold),
            ElasticBreathState.Resting => snapshot.RestingProgressRatio(_settings.RestOvertimeThreshold),
            _ => 0
        };

        _overlayWindow.UpdateOverlay(
            overlayState,
            _settings.EnableEdgeGlow,
            _settings.EnableTopProgressBar,
            topProgressRatio,
            _settings.GlowMaxThicknessPixels,
            _settings.OverlayOpacity,
            hideForFullscreen);

        if (snapshot.PendingTransition is null)
        {
            _notificationWindow.Hide();
        }
        else
        {
            _notificationWindow.UpdateMessage(snapshot.PendingTransition.Message, snapshot.PendingTransition.Remaining);
            if (!_notificationWindow.IsVisible)
            {
                _notificationWindow.Show();
            }
        }
    }

    private static EdgeOverlayState ResolveOverlayState(EngineSnapshot snapshot)
        => snapshot.State switch
        {
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Warning => EdgeOverlayState.Warning,
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Hard => EdgeOverlayState.Hard,
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Base => EdgeOverlayState.RestBase,
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Elastic => EdgeOverlayState.RestElastic,
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Overtime => EdgeOverlayState.RestOvertime,
            ElasticBreathState.Paused => EdgeOverlayState.Paused,
            _ => EdgeOverlayState.Hidden
        };

    private static MediaColor ResolveProgressColor(EngineSnapshot snapshot)
        => snapshot.State switch
        {
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Safe => MediaColor.FromRgb(50, 178, 101),
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Warning => MediaColor.FromRgb(237, 144, 57),
            ElasticBreathState.Working => MediaColor.FromRgb(224, 64, 64),
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Base => MediaColor.FromRgb(54, 179, 102),
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Elastic => MediaColor.FromRgb(41, 201, 105),
            ElasticBreathState.Resting => MediaColor.FromRgb(35, 169, 94),
            ElasticBreathState.Paused => MediaColor.FromRgb(130, 130, 130),
            _ => MediaColor.FromRgb(120, 160, 140)
        };

    private static string FormatDuration(TimeSpan span, bool shortStyle = false)
    {
        return shortStyle
            ? $"{(int)span.TotalMinutes:00}:{span.Seconds:00}"
            : $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
    }

    private static Geometry BuildArcGeometry(System.Windows.Point center, double radius, double progressRatio)
    {
        progressRatio = Math.Clamp(progressRatio, 0, 1);
        if (progressRatio <= 0)
        {
            return Geometry.Empty;
        }

        if (progressRatio >= 0.9999)
        {
            return new EllipseGeometry(center, radius, radius);
        }

        var startAngle = -90d;
        var endAngle = startAngle + (360d * progressRatio);
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, endAngle);
        var largeArc = progressRatio > 0.5;

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, largeArc, SweepDirection.Clockwise, true));
        return new PathGeometry(new[] { figure });
    }

    private static System.Windows.Point PointOnCircle(System.Windows.Point center, double radius, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180d;
        return new System.Windows.Point(
            center.X + (radius * Math.Cos(rad)),
            center.Y + (radius * Math.Sin(rad)));
    }

    private void ApplySettingsToControls()
    {
        MinWorkBox.Text = _settings.MinWorkMinutes.ToString();
        MaxWorkBox.Text = _settings.MaxWorkMinutes.ToString();
        DefaultRestBox.Text = _settings.DefaultRestMinutes.ToString();
        OvertimeRestBox.Text = _settings.RestOvertimeMinutes.ToString();

        EdgeGlowToggle.IsChecked = _settings.EnableEdgeGlow;
        TopBarToggle.IsChecked = _settings.EnableTopProgressBar;
        CornerHoverToggle.IsChecked = _settings.EnableCornerHover;
        FullscreenHideToggle.IsChecked = _settings.FullscreenHideMode;
        SoundToggle.IsChecked = _settings.EnableSound;
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettingsFromControls(out var error))
        {
            MessageBox.Show(error, "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.Sanitize();
        _settingsStore.Save(_settings);
        ApplySettingsToControls();
        UpdateOverlayAndNotifications(_engine.Snapshot);
        PendingText.Text = "Pending transition: settings saved";
    }

    private bool TryReadSettingsFromControls(out string error)
    {
        error = string.Empty;
        if (!int.TryParse(MinWorkBox.Text, out var minWork)
            || !int.TryParse(MaxWorkBox.Text, out var maxWork)
            || !int.TryParse(DefaultRestBox.Text, out var defaultRest)
            || !int.TryParse(OvertimeRestBox.Text, out var overtimeRest))
        {
            error = "Time parameters must be integers.";
            return false;
        }

        _settings.MinWorkMinutes = minWork;
        _settings.MaxWorkMinutes = maxWork;
        _settings.DefaultRestMinutes = defaultRest;
        _settings.RestOvertimeMinutes = overtimeRest;
        _settings.EnableEdgeGlow = EdgeGlowToggle.IsChecked == true;
        _settings.EnableTopProgressBar = TopBarToggle.IsChecked == true;
        _settings.EnableCornerHover = CornerHoverToggle.IsChecked == true;
        _settings.FullscreenHideMode = FullscreenHideToggle.IsChecked == true;
        _settings.EnableSound = SoundToggle.IsChecked == true;
        return true;
    }

    private void StartWorkButton_Click(object sender, RoutedEventArgs e) => _engine.StartWorkingManual();

    private void StartRestButton_Click(object sender, RoutedEventArgs e) => _engine.StartRestingManual();

    private void StopButton_Click(object sender, RoutedEventArgs e) => _engine.StopToIdle();

    private void PauseRemindersButton_Click(object sender, RoutedEventArgs e)
        => _engine.SetRemindersPaused(!_engine.Snapshot.RemindersPaused);
}
