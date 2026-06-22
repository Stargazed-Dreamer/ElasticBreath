using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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
    private readonly LocalizationService _localization;
    private readonly BreathEngine _engine;
    private readonly CornerTriggerService _cornerTrigger;
    private readonly DisplayTargetService _displayTargetService;
    private readonly SecondaryMonitorFlashService _secondaryFlashService;
    private readonly SessionMonitor _sessionMonitor;
    private readonly EdgeOverlayWindow _overlayWindow;
    private readonly CountdownNotificationWindow _notificationWindow;
    private readonly ToastWindow _toastWindow;
    private readonly System.Windows.Threading.DispatcherTimer _cornerPollTimer;

    private Forms.NotifyIcon? _trayIcon;
    private bool _exitRequested;
    private IntPtr _mainWindowHandle;

    public MainWindow()
    {
        InitializeComponent();

        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
        _localization = new LocalizationService();
        _localization.Load(_settings.Language);

        var remoteFilter = new RemoteInputFilterService();
        _engine = new BreathEngine(_settings, new InputMonitor(remoteFilter));
        _cornerTrigger = new CornerTriggerService();
        _displayTargetService = new DisplayTargetService();
        _secondaryFlashService = new SecondaryMonitorFlashService();
        _sessionMonitor = new SessionMonitor();
        _overlayWindow = new EdgeOverlayWindow();
        _notificationWindow = new CountdownNotificationWindow();
        _toastWindow = new ToastWindow();
        _cornerPollTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        _engine.SnapshotChanged += OnSnapshotChanged;
        _notificationWindow.CancelRequested += (_, _) => _engine.CancelPendingTransition();
        _cornerPollTimer.Tick += OnCornerPollTimerTick;
        _sessionMonitor.SessionLockChanged += (_, locked) => _engine.HandleSessionSwitch(locked);

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _mainWindowHandle = new WindowInteropHelper(this).Handle;
        InitializeTrayIcon();
        ApplyLocalization();
        _engine.Start();
        _cornerPollTimer.Start();
        RenderSnapshot(_engine.Snapshot);
        UpdateOverlayAndNotifications(_engine.Snapshot);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        if (_settings.CloseBehaviorOnMainWindowClose == CloseBehavior.Exit)
        {
            _exitRequested = true;
            return;
        }

        e.Cancel = true;
        Hide();
        _trayIcon?.ShowBalloonTip(
            1400,
            _localization.T("tray.minimized_title"),
            _localization.T("tray.minimized_body"),
            Forms.ToolTipIcon.Info);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _cornerPollTimer.Stop();
        _engine.Dispose();
        _overlayWindow.Close();
        _notificationWindow.Close();
        _toastWindow.Close();
        _secondaryFlashService.Dispose();
        _sessionMonitor.Dispose();

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

        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resource", "icon.ico");
        var icon = System.IO.File.Exists(iconPath) ? new Drawing.Icon(iconPath) : Drawing.SystemIcons.Application;

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Visible = true
        };
        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                RestoreFromTray();
            }
        };
        RebuildTrayMenu();
    }

    private void RebuildTrayMenu()
    {
        if (_trayIcon is null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_localization.T("tray.open"), null, (_, _) => RestoreFromTray());
        menu.Items.Add(_localization.T("tray.exit"), null, (_, _) => ExitFromTray());

        _trayIcon.Text = _localization.T("tray.tip");
        _trayIcon.ContextMenuStrip = menu;
    }

    private void RestoreFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
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
        var b = targetScreen.Bounds;
        var bounds = new Rect(b.Left, b.Top, b.Width, b.Height);
        var cursor = Forms.Cursor.Position;
        if (_cornerTrigger.TryTrigger(bounds, new System.Windows.Point(cursor.X, cursor.Y), _settings.CornerHoverDuration))
        {
            var state = _engine.TriggerCornerTransition();
            _toastWindow.ShowMessage(
                _localization.Tf("notify.corner_switched", ResolveStateText(state)),
                new Rect(b.Left, b.Top, b.Width, b.Height));
        }
    }

    private void ApplyLocalization()
    {
        Title = _localization.T("app.title");
        AppNameText.Text = _localization.T("app.name");
        AppSubtitleText.Text = _localization.T("app.subtitle");
        TodayTitleText.Text = _localization.T("today.title");
        CornerHintText.Text = _localization.T("hint.corner");
        TrayHintText.Text = _localization.T("hint.tray");

        StartWorkButton.Content = _localization.T("button.start_work");
        StartRestButton.Content = _localization.T("button.start_rest");
        StopButton.Content = _localization.T("button.stop_idle");
        OpenSettingsButton.Content = _localization.T("button.open_settings");
        OpenHelpButton.Content = _localization.T("button.open_help");
        RebuildTrayMenu();

        /* 重新渲染以更新动态按钮文本 */
        RenderSnapshot(_engine.Snapshot);
    }

    private void RenderSnapshot(EngineSnapshot snapshot)
    {
        var stateText = ResolveStateText(snapshot.State);
        StateText.Text = _localization.Tf("state.prefix", stateText);
        CenterStateText.Text = ResolveCenterStateText(snapshot.State);
        TodayWorkText.Text = _localization.Tf("today.work", FormatDuration(snapshot.TotalWorkingToday, false));
        TodayRestText.Text = _localization.Tf("today.rest", FormatDuration(snapshot.TotalRestingToday, false));
        PauseRemindersButton.Content = snapshot.RemindersPaused
            ? _localization.T("button.resume_reminders")
            : _localization.T("button.pause_reminders");

        /* 根据当前状态动态切换按钮文本：
           - 工作中 → "暂停"，休息中 → "暂停"
           - 暂停中 → 只有暂停前对应的按钮显示"继续"，另一个显示"开始工作/开始休息"
           - 其他状态 → "开始工作"/"开始休息" */
        if (snapshot.State == ElasticBreathState.Paused)
        {
            var fromWorking = snapshot.StateBeforePause == ElasticBreathState.Working;
            StartWorkButton.Content = fromWorking
                ? _localization.T("button.resume_work")
                : _localization.T("button.start_work");
            StartRestButton.Content = fromWorking
                ? _localization.T("button.start_rest")
                : _localization.T("button.resume_rest");
        }
        else
        {
            StartWorkButton.Content = snapshot.State == ElasticBreathState.Working
                ? _localization.T("button.pause_work")
                : _localization.T("button.start_work");
            StartRestButton.Content = snapshot.State == ElasticBreathState.Resting
                ? _localization.T("button.pause_rest")
                : _localization.T("button.start_rest");
        }

        if (snapshot.PendingTransition is null)
        {
            PendingText.Text = _localization.T("pending.none");
        }
        else
        {
            var seconds = Math.Max(0, (int)Math.Ceiling(snapshot.PendingTransition.Remaining.TotalSeconds));
            PendingText.Text = _localization.Tf("pending.format", ResolveTransitionText(snapshot.PendingTransition.Kind), seconds);
        }

        /* 显示智能检测探测进度（如"持续活动 2s/4s 后开始工作"） */
        if (snapshot.DetectionProbe is not null)
        {
            var probeElapsed = (int)Math.Floor(snapshot.DetectionProbe.Elapsed.TotalSeconds);
            var probeRequired = (int)Math.Ceiling(snapshot.DetectionProbe.Required.TotalSeconds);
            PendingText.Text = _localization.Tf(snapshot.DetectionProbe.MessageKey, probeElapsed, probeRequired);
        }

        if (snapshot.SessionLocked)
        {
            PendingText.Text = _localization.T("status.session_locked");
        }

        var elapsed = snapshot.State switch
        {
            ElasticBreathState.Resting => snapshot.RestingCycleElapsed,
            _ => snapshot.WorkingCycleElapsed
        };
        TimerText.Text = FormatDuration(elapsed, true);
        PressureText.Text = _localization.Tf("pressure.prefix", ResolvePressureText(snapshot));

        var progress = snapshot.State switch
        {
            ElasticBreathState.Working => snapshot.WorkingProgressRatio(_settings.MaxWorkThreshold),
            ElasticBreathState.Resting => snapshot.RestingProgressRatio(_settings.RestOvertimeThreshold),
            _ => 0
        };
        ProgressArc.Stroke = new SolidColorBrush(ResolveProgressColor(snapshot));
        ProgressArc.Data = BuildArcGeometry(new System.Windows.Point(140, 140), 106, progress);
    }

    private void UpdateOverlayAndNotifications(EngineSnapshot snapshot)
    {
        var targetScreen = _displayTargetService.GetTargetScreen(_settings.PreferredDisplay);
        MonitorText.Text = _localization.Tf("monitor.target", targetScreen.DeviceName);

        _overlayWindow.SetBounds(targetScreen.Bounds);
        _overlayWindow.ConfigureReTopmost(_settings.EnablePeriodicReTopmost, _settings.ReTopmostIntervalSeconds);
        _notificationWindow.PositionAtTopRight(targetScreen.Bounds);

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
            ResolveGlowThickness(snapshot),
            _settings.OverlayOpacity,
            hideForFullscreen);

        _secondaryFlashService.Update(
            _settings,
            targetScreen,
            overlayState,
            hideForFullscreen || !_settings.EnableEdgeGlow,
            !_displayTargetService.IsCursorOnScreen(targetScreen));

        if (snapshot.PendingTransition is null)
        {
            _notificationWindow.Hide();
        }
        else
        {
            var message = _localization.Tf(snapshot.PendingTransition.MessageKey, _settings.AutoTransitionCountdownSeconds);
            _notificationWindow.UpdateMessage(message, _localization.T("notify.auto_action"), snapshot.PendingTransition.Remaining);
            if (!_notificationWindow.IsVisible)
            {
                _notificationWindow.Show();
            }
        }
    }

    private int ResolveGlowThickness(EngineSnapshot snapshot)
    {
        var max = _settings.GlowMaxThicknessPixels;
        var baseThickness = Math.Clamp(max / 3, 12, max);

        /* Hard 警示：工作超过最大阈值后，渗透宽度逐步增长到上限 */
        if (snapshot.State == ElasticBreathState.Working && snapshot.WorkingPressure == WorkingPressureLevel.Hard)
        {
            var overtime = snapshot.WorkingCycleElapsed - _settings.MaxWorkThreshold;
            var ratio = Math.Clamp(overtime.TotalSeconds / 120d, 0, 1);
            return (int)Math.Round(baseThickness + ((max - baseThickness) * ratio));
        }

        /* 休息超时：渗透宽度逐步增长到上限 */
        if (snapshot.State == ElasticBreathState.Resting && snapshot.RestPressure == RestPressureLevel.Overtime)
        {
            var overtime = snapshot.RestingCycleElapsed - _settings.RestOvertimeThreshold;
            var ratio = Math.Clamp(overtime.TotalSeconds / 120d, 0, 1);
            return (int)Math.Round(baseThickness + ((max - baseThickness) * ratio));
        }

        return baseThickness;
    }

    private string ResolveStateText(ElasticBreathState state)
        => state switch
        {
            ElasticBreathState.Idle => _localization.T("state.idle"),
            ElasticBreathState.Working => _localization.T("state.working"),
            ElasticBreathState.Paused => _localization.T("state.paused"),
            ElasticBreathState.Resting => _localization.T("state.resting"),
            _ => state.ToString()
        };

    private string ResolveCenterStateText(ElasticBreathState state)
        => state switch
        {
            ElasticBreathState.Idle => _localization.T("center.state.idle"),
            ElasticBreathState.Working => _localization.T("center.state.working"),
            ElasticBreathState.Paused => _localization.T("center.state.paused"),
            ElasticBreathState.Resting => _localization.T("center.state.resting"),
            _ => state.ToString().ToUpperInvariant()
        };

    private string ResolvePressureText(EngineSnapshot snapshot)
        => snapshot.State switch
        {
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Safe => _localization.T("pressure.safe"),
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Warning => _localization.T("pressure.warning"),
            ElasticBreathState.Working => _localization.T("pressure.hard"),
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Base => _localization.T("pressure.base"),
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Elastic => _localization.T("pressure.elastic"),
            ElasticBreathState.Resting => _localization.T("pressure.overtime"),
            ElasticBreathState.Paused => _localization.T("pressure.paused"),
            _ => _localization.T("pressure.idle")
        };

    private string ResolveTransitionText(PendingTransitionKind kind)
        => kind switch
        {
            PendingTransitionKind.IdleToWorking => _localization.T("transition.idle_to_working"),
            PendingTransitionKind.WorkingToPaused => _localization.T("transition.working_to_paused"),
            PendingTransitionKind.PausedToWorking => _localization.T("transition.paused_to_working"),
            PendingTransitionKind.WorkingToResting => _localization.T("transition.working_to_resting"),
            PendingTransitionKind.RestingToWorking => _localization.T("transition.resting_to_working"),
            _ => kind.ToString()
        };

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

    private static string FormatDuration(TimeSpan span, bool shortStyle)
        => shortStyle
            ? $"{(int)span.TotalMinutes:00}:{span.Seconds:00}"
            : $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";

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
        figure.Segments.Add(new ArcSegment(end, new System.Windows.Size(radius, radius), 0, largeArc, SweepDirection.Clockwise, true));
        return new PathGeometry(new[] { figure });
    }

    private static System.Windows.Point PointOnCircle(System.Windows.Point center, double radius, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180d;
        return new System.Windows.Point(center.X + (radius * Math.Cos(rad)), center.Y + (radius * Math.Sin(rad)));
    }

    private void StartWorkButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_engine.Snapshot.State)
        {
            case ElasticBreathState.Working:
                _engine.PauseFromWorking();
                break;
            case ElasticBreathState.Paused:
                if (_engine.Snapshot.StateBeforePause == ElasticBreathState.Working)
                {
                    /* 从工作暂停 → 继续工作 */
                    _engine.ResumeWorking();
                }
                else
                {
                    /* 从休息暂停 → 直接开始工作（切换状态） */
                    _engine.StartWorkingManual();
                }
                break;
            default:
                _engine.StartWorkingManual();
                break;
        }
    }

    private void StartRestButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_engine.Snapshot.State)
        {
            case ElasticBreathState.Resting:
                _engine.PauseFromResting();
                break;
            case ElasticBreathState.Paused:
                if (_engine.Snapshot.StateBeforePause == ElasticBreathState.Resting)
                {
                    /* 从休息暂停 → 继续休息 */
                    _engine.ResumeResting();
                }
                else
                {
                    /* 从工作暂停 → 直接开始休息（切换状态） */
                    _engine.StartRestingManual();
                }
                break;
            default:
                _engine.StartRestingManual();
                break;
        }
    }
    private void StopButton_Click(object sender, RoutedEventArgs e) => _engine.StopToIdle();
    private void PauseRemindersButton_Click(object sender, RoutedEventArgs e) => _engine.SetRemindersPaused(!_engine.Snapshot.RemindersPaused);

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings, _localization) { Owner = this };
        dialog.SettingsApplied += (_, _) => ApplySettings();
        _ = dialog.ShowDialog();
    }

    private void ApplySettings()
    {
        _settingsStore.Save(_settings);
        _localization.Load(_settings.Language);
        ApplyLocalization();
        PendingText.Text = _localization.T("status.settings_saved");
        RenderSnapshot(_engine.Snapshot);
        UpdateOverlayAndNotifications(_engine.Snapshot);
    }

    private void OpenHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HelpWindow(_localization) { Owner = this };
        _ = dialog.ShowDialog();
    }
}
