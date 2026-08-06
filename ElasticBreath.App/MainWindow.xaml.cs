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

/// <summary>
/// 应用程序的主窗口类，负责管理界面显示、系统托盘交互、引擎状态监控以及各种用户操作的处理。
/// </summary>
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

    /// <summary>
    /// 主窗口构造函数，负责初始化应用程序所需的核心服务、引擎、UI组件和事件订阅。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        // 初始化并加载应用程序设置
        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
        // 根据设置加载对应语言包，实现界面本地化
        _localization = new LocalizationService();
        _localization.Load(_settings.Language);

        // 创建远程输入过滤服务
        var remoteFilter = new RemoteInputFilterService();
        // 初始化呼吸检测引擎，关联设置和输入监控器（包含远程过滤）
        _engine = new BreathEngine(_settings, new InputMonitor(remoteFilter));
        // 初始化屏幕角落触发服务
        _cornerTrigger = new CornerTriggerService();
        // 初始化显示目标服务，用于确定交互目标屏幕
        _displayTargetService = new DisplayTargetService();
        // 初始化副屏闪烁提醒服务
        _secondaryFlashService = new SecondaryMonitorFlashService();
        // 初始化用户会话监控，跟踪系统锁定/解锁状态
        _sessionMonitor = new SessionMonitor();
        // 初始化屏幕边缘覆盖层窗口
        _overlayWindow = new EdgeOverlayWindow();
        // 初始化倒计时通知窗口
        _notificationWindow = new CountdownNotificationWindow();
        // 初始化提示消息窗口
        _toastWindow = new ToastWindow();
        // 初始化屏幕角落轮询定时器，每250毫秒检查一次鼠标位置
        _cornerPollTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        // 订阅引擎状态快照更新事件
        _engine.SnapshotChanged += OnSnapshotChanged;
        // 订阅通知窗口的取消请求事件，用于中止待处理的转换操作
        _notificationWindow.CancelRequested += (_, _) => _engine.CancelPendingTransition();
        // 订阅角落轮询定时器的触发事件
        _cornerPollTimer.Tick += OnCornerPollTimerTick;
        // 订阅会话锁定状态变更事件，并通知引擎处理会话切换
        _sessionMonitor.SessionLockChanged += (_, locked) => _engine.HandleSessionSwitch(locked);

        // 订阅窗口生命周期事件
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    /// <summary>
    /// 当窗口加载完成时调用的事件处理方法。用于初始化窗口相关组件、启动必要服务并设置初始状态。
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 获取当前窗口的句柄，用于后续系统级交互
        _mainWindowHandle = new WindowInteropHelper(this).Handle;
        // 初始化系统托盘图标
        InitializeTrayIcon();
        // 应用本地化设置，根据当前区域调整界面语言
        ApplyLocalization();
        // 启动数据处理引擎
        _engine.Start();
        // 启动角落检测的轮询计时器，用于监控窗口位置
        _cornerPollTimer.Start();
        // 使用引擎的快照数据渲染初始界面状态
        RenderSnapshot(_engine.Snapshot);
        // 根据引擎快照更新覆盖层和通知信息
        UpdateOverlayAndNotifications(_engine.Snapshot);
    }

    /// <summary>
    /// 处理窗口关闭事件，根据设置决定是否退出或最小化到系统托盘。
    /// </summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // 如果已经请求退出，则直接返回，避免重复处理
        if (_exitRequested)
        {
            return;
        }

        // 如果设置为关闭时退出，则设置退出标志并返回
        if (_settings.CloseBehaviorOnMainWindowClose == CloseBehavior.Exit)
        {
            _exitRequested = true;
            return;
        }

        // 取消关闭操作，隐藏窗口，并显示托盘气泡提示
        e.Cancel = true;
        Hide();
        _trayIcon?.ShowBalloonTip(
            1400,
            _localization.T("tray.minimized_title"),
            _localization.T("tray.minimized_body"),
            Forms.ToolTipIcon.Info);
    }

    /// <summary>
    /// 窗口关闭时执行的清理操作
    /// 停止计时器、释放资源、关闭所有子窗口并清理系统托盘图标
    /// </summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        // 停止角落位置轮询计时器
        _cornerPollTimer.Stop();
        // 释放图形处理引擎资源
        _engine.Dispose();
        // 关闭覆盖窗口
        _overlayWindow.Close();
        // 关闭通知窗口
        _notificationWindow.Close();
        // 关闭提示窗口
        _toastWindow.Close();
        // 释放二级闪烁服务资源
        _secondaryFlashService.Dispose();
        // 释放会话监控器资源
        _sessionMonitor.Dispose();

        // 如果系统托盘图标存在则进行清理
        if (_trayIcon is not null)
        {
            // 隐藏托盘图标
            _trayIcon.Visible = false;
            // 释放托盘图标资源
            _trayIcon.Dispose();
            // 将引用置空以协助垃圾回收
            _trayIcon = null;
        }
    }

    /// <summary>
    /// 初始化系统托盘图标，包括图标加载、事件绑定和菜单重建
    /// </summary>
    private void InitializeTrayIcon()
    {
        // 防止重复初始化，如果托盘图标已存在则直接返回
        if (_trayIcon is not null)
        {
            return;
        }

        // 获取图标文件路径：程序目录下Resource文件夹中的icon.ico
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resource", "icon.ico");
        // 如果图标文件存在则加载，否则使用系统默认应用程序图标
        var icon = System.IO.File.Exists(iconPath) ? new Drawing.Icon(iconPath) : Drawing.SystemIcons.Application;

        // 创建NotifyIcon实例并设置基本属性
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = icon,    // 设置托盘图标
            Visible = true  // 使图标在系统托盘中可见
        };

        // 为托盘图标绑定鼠标点击事件处理
        _trayIcon.MouseClick += (_, args) =>
        {
            // 当用户左键单击托盘图标时恢复主窗口
            if (args.Button == Forms.MouseButtons.Left)
            {
                RestoreFromTray();
            }
        };

        // 重建托盘图标的右键菜单
        RebuildTrayMenu();
    }

    /// <summary>
    /// 重建系统托盘菜单，包括创建菜单、添加菜单项和设置托盘图标属性。
    /// </summary>
    private void RebuildTrayMenu()
    {
        // 检查托盘图标是否为空，如果是则直接返回
        if (_trayIcon is null)
        {
            return;
        }

        // 创建新的上下文菜单
        var menu = new Forms.ContextMenuStrip();
        // 添加打开菜单项，点击时恢复窗口
        menu.Items.Add(_localization.T("tray.open"), null, (_, _) => RestoreFromTray());
        // 添加退出菜单项，点击时退出应用
        menu.Items.Add(_localization.T("tray.exit"), null, (_, _) => ExitFromTray());

        // 设置托盘图标提示文本
        _trayIcon.Text = _localization.T("tray.tip");
        // 将上下文菜单关联到托盘图标
        _trayIcon.ContextMenuStrip = menu;
    }

    /// <summary>
    /// 从系统托盘恢复窗口并激活它。
    /// </summary>
    private void RestoreFromTray()
    {
        // 显示窗口
        Show();
        // 检查窗口状态是否为最小化
        if (WindowState == WindowState.Minimized)
        {
            // 如果是，将窗口状态设置为正常
            WindowState = WindowState.Normal;
        }
        // 激活窗口以获取焦点
        Activate();
    }

    /// <summary>
    /// 从系统托盘退出应用程序，设置退出请求标志并关闭窗口。
    /// </summary>
    private void ExitFromTray()
    {
        // 设置退出请求标志为真，表示应用请求退出
        _exitRequested = true;
        // 关闭当前窗口，触发退出流程
        Close();
    }

    /// <summary>
    /// 处理引擎快照变化事件的方法。
    /// 当快照数据发生更新时调用此方法，负责渲染新的快照并更新界面相关元素。
    /// </summary>
    /// <param name="sender">事件发送者对象，可能为空</param>
    /// <param name="snapshot">包含最新数据的引擎快照对象</param>
    private void OnSnapshotChanged(object? sender, EngineSnapshot snapshot)
    {
        // 当引擎快照发生变化时触发此事件处理方法
        // 渲染接收到的快照数据
        RenderSnapshot(snapshot);
        // 根据快照状态更新界面覆盖层和系统通知
        UpdateOverlayAndNotifications(snapshot);
    }

    /// <summary>
    /// 当角落轮询定时器触发时调用，处理角落悬停功能的检测和状态切换。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">事件参数。</param>
    private void OnCornerPollTimerTick(object? sender, EventArgs e)
    {
        // 如果角落悬停功能未启用，则直接返回
        if (!_settings.EnableCornerHover)
        {
            return;
        }

        // 获取引擎的当前快照
        var snapshot = _engine.Snapshot;
        // 检查引擎状态是否为工作或休息状态，如果不是则返回
        if (snapshot.State is not (ElasticBreathState.Working or ElasticBreathState.Resting))
        {
            return;
        }

        // 获取目标显示屏幕的边界
        var targetScreen = _displayTargetService.GetTargetScreen(_settings.PreferredDisplay);
        var b = targetScreen.Bounds;
        var bounds = new Rect(b.Left, b.Top, b.Width, b.Height);
        // 获取当前光标位置
        var cursor = Forms.Cursor.Position;
        // 检查角落悬停触发条件是否满足
        if (_cornerTrigger.TryTrigger(bounds, new System.Windows.Point(cursor.X, cursor.Y), _settings.CornerHoverDuration))
        {
            // 触发角落状态转换
            var state = _engine.TriggerCornerTransition();
            // 显示状态切换的消息通知
            _toastWindow.ShowMessage(
                _localization.Tf("notify.corner_switched", ResolveStateText(state)),
                new Rect(b.Left, b.Top, b.Width, b.Height));
        }
    }

    /// <summary>
    /// 应用本地化设置，更新界面中所有文本元素的语言显示。
    /// </summary>
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
        PostponeButton.Content = _localization.T("button.postpone");
        StopButton.Content = _localization.T("button.stop_idle");
        OpenSettingsButton.Content = _localization.T("button.open_settings");
        OpenHelpButton.Content = _localization.T("button.open_help");
        RebuildTrayMenu();

        /* 重新渲染以更新动态按钮文本 */
        RenderSnapshot(_engine.Snapshot);
    }

    /// <summary>
    /// 根据提供的引擎快照对象，渲染并更新UI界面的状态信息。
    /// </summary>
    /// <param name="snapshot">引擎快照对象，包含当前状态、计时器、待处理事件等数据。</param>
    private void RenderSnapshot(EngineSnapshot snapshot)
    {
        // 解析状态文本并设置到UI元素
        var stateText = ResolveStateText(snapshot.State);
        StateText.Text = _localization.Tf("state.prefix", stateText);
        CenterStateText.Text = ResolveCenterStateText(snapshot.State);
        // 设置今日工作和休息时间
        TodayWorkText.Text = _localization.Tf("today.work", FormatDuration(snapshot.TotalWorkingToday, false));
        TodayRestText.Text = _localization.Tf("today.rest", FormatDuration(snapshot.TotalRestingToday, false));
        // 根据提醒是否暂停，切换按钮文本
        PauseRemindersButton.Content = snapshot.RemindersPaused
            ? _localization.T("button.resume_reminders")
            : _localization.T("button.pause_reminders");

        /* 推迟按钮：仅在 Working 且压力为 Warning/Hard、未冷却、配额未用完时启用，
           按钮文本附带剩余次数；冷却中显示剩余冷却时间 */
        var postpone = snapshot.Postpone;
        PostponeButton.IsEnabled = postpone.CanPostpone;
        if (postpone.CooldownRemaining > TimeSpan.Zero)
        {
            PostponeButton.Content = _localization.Tf("button.postpone_cooldown", (int)Math.Ceiling(postpone.CooldownRemaining.TotalSeconds));
        }
        else
        {
            PostponeButton.Content = _localization.Tf("button.postpone_count", postpone.PostponesRemainingToday, postpone.DailyLimit);
        }
        PostponeButton.ToolTip = _localization.Tf("tooltip.postpone", postpone.PostponesUsedToday, postpone.DailyLimit);

        /* 根据当前状态动态切换按钮文本：
           - 工作中 → "暂停"，休息中 → "暂停"
           - 暂停中 → 只有暂停前对应的按钮显示"继续"，另一个显示"开始工作/开始休息"
           - 其他状态 → "开始工作"/"开始休息" */
        if (snapshot.State == ElasticBreathState.Paused)
        {
            // 暂停状态：根据暂停前的状态决定按钮文本
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
            // 非暂停状态：根据当前状态切换按钮文本
            StartWorkButton.Content = snapshot.State == ElasticBreathState.Working
                ? _localization.T("button.pause_work")
                : _localization.T("button.start_work");
            StartRestButton.Content = snapshot.State == ElasticBreathState.Resting
                ? _localization.T("button.pause_rest")
                : _localization.T("button.start_rest");
        }

        // 检查是否有待处理的状态转换
        if (snapshot.PendingTransition is null)
        {
            PendingText.Text = _localization.T("pending.none");
        }
        else
        {
            // 计算剩余秒数并显示转换信息
            var seconds = Math.Max(0, (int)Math.Ceiling(snapshot.PendingTransition.Remaining.TotalSeconds));
            PendingText.Text = _localization.Tf("pending.format", ResolveTransitionText(snapshot.PendingTransition.Kind), seconds);
        }

        /* 显示智能检测探测进度（如"持续活动 2s/4s 后开始工作"） */
        if (snapshot.DetectionProbe is not null)
        {
            // 计算探测进度
            var probeElapsed = (int)Math.Floor(snapshot.DetectionProbe.Elapsed.TotalSeconds);
            var probeRequired = (int)Math.Ceiling(snapshot.DetectionProbe.Required.TotalSeconds);
            PendingText.Text = _localization.Tf(snapshot.DetectionProbe.MessageKey, probeElapsed, probeRequired);
        }

        // 如果会话被锁定，显示锁定状态
        if (snapshot.SessionLocked)
        {
            PendingText.Text = _localization.T("status.session_locked");
        }

        // 根据当前状态获取已用时间
        var elapsed = snapshot.State switch
        {
            ElasticBreathState.Resting => snapshot.RestingCycleElapsed,
            _ => snapshot.WorkingCycleElapsed
        };
        // 格式化并显示时间
        TimerText.Text = FormatDuration(elapsed, true);
        // 显示压力文本
        PressureText.Text = _localization.Tf("pressure.prefix", ResolvePressureText(snapshot));

        // 计算进度比例
        var progress = snapshot.State switch
        {
            ElasticBreathState.Working => snapshot.WorkingProgressRatio(_settings.MaxWorkThreshold),
            ElasticBreathState.Resting => snapshot.RestingProgressRatio(_settings.RestOvertimeThreshold),
            _ => 0
        };
        // 更新进度弧的颜色和几何形状
        ProgressArc.Stroke = new SolidColorBrush(ResolveProgressColor(snapshot));
        ProgressArc.Data = BuildArcGeometry(new System.Windows.Point(140, 140), 106, progress);
    }

    /// <summary>
    /// 更新覆盖窗口和通知窗口的状态与显示。
    /// 该方法根据引擎快照信息，计算覆盖窗口的目标屏幕、布局、样式及可见性，并相应地更新通知内容。
    /// </summary>
    /// <param name="snapshot">引擎状态快照，包含当前呼吸状态、进度比例等信息。</param>
    private void UpdateOverlayAndNotifications(EngineSnapshot snapshot)
    {
        // 获取目标显示屏幕（基于用户设置的首选显示器）
        var targetScreen = _displayTargetService.GetTargetScreen(_settings.PreferredDisplay);
        // 更新监控目标信息文本
        MonitorText.Text = _localization.Tf("monitor.target", targetScreen.DeviceName);

        // 设置覆盖窗口边界为目标屏幕的尺寸和位置
        _overlayWindow.SetBounds(targetScreen.Bounds);
        // 根据设置配置覆盖窗口的定期置顶行为
        _overlayWindow.ConfigureReTopmost(_settings.EnablePeriodicReTopmost, _settings.ReTopmostIntervalSeconds);
        // 将通知窗口定位到目标屏幕的右上角
        _notificationWindow.PositionAtTopRight(targetScreen.Bounds);

        // 判断是否因全屏应用而需隐藏覆盖窗口（根据设置和当前前台全屏状态）
        var hideForFullscreen = _settings.FullscreenHideMode && _displayTargetService.IsFullscreenForeground(targetScreen, _mainWindowHandle);
        // 根据快照解析覆盖窗口应显示的状态
        var overlayState = ResolveOverlayState(snapshot);
        // 根据当前呼吸状态计算顶部进度条比例
        var topProgressRatio = snapshot.State switch
        {
            ElasticBreathState.Working => snapshot.WorkingProgressRatio(_settings.MaxWorkThreshold), // 工作状态：计算工作进度
            ElasticBreathState.Resting => snapshot.RestingProgressRatio(_settings.RestOvertimeThreshold), // 休息状态：计算休息进度
            _ => 0 // 其他状态：进度为0
        };

        // 更新覆盖窗口的显示状态和样式
        _overlayWindow.UpdateOverlay(
            overlayState, // 覆盖窗口状态
            _settings.EnableEdgeGlow, // 是否启用边缘发光
            _settings.EnableTopProgressBar, // 是否启用顶部进度条
            topProgressRatio, // 顶部进度条比例
            ResolveGlowThickness(snapshot), // 根据快照解析发光厚度
            _settings.OverlayOpacity, // 覆盖窗口不透明度
            hideForFullscreen); // 是否因全屏而隐藏

        // 更新二级闪光服务（可能用于额外视觉提示）
        _secondaryFlashService.Update(
            _settings, // 应用设置
            targetScreen, // 目标屏幕
            overlayState, // 覆盖状态
            hideForFullscreen || !_settings.EnableEdgeGlow, // 满足隐藏条件或未启用边缘发光时禁用闪光
            !_displayTargetService.IsCursorOnScreen(targetScreen)); // 判断鼠标光标是否不在目标屏幕上

        // 处理待处理的状态转换通知
        if (snapshot.PendingTransition is null)
        {
            // 无待处理转换时，隐藏通知窗口
            _notificationWindow.Hide();
        }
        else
        {
            // 有待处理转换时，准备通知消息
            // 根据转换消息键和设置的倒计时秒数，获取本地化消息
            var message = _localization.Tf(snapshot.PendingTransition.MessageKey, _settings.AutoTransitionCountdownSeconds);
            // 更新通知窗口的消息内容、操作提示和剩余时间
            _notificationWindow.UpdateMessage(message, _localization.T("notify.auto_action"), snapshot.PendingTransition.Remaining);
            // 如果通知窗口当前不可见，则显示它
            if (!_notificationWindow.IsVisible)
            {
                _notificationWindow.Show();
            }
        }
    }

    /// <summary>
    /// 计算并返回光晕的厚度值（像素）。
    /// 该方法根据引擎快照的当前状态和工作/休息压力级别，
    /// 动态决定光晕厚度。当处于工作超限或休息超时状态时，
    /// 厚度会根据超时程度逐渐增长至最大值。
    /// </summary>
    /// <param name="snapshot">包含当前引擎状态、工作/休息时间等信息的快照对象</param>
    /// <returns>光晕厚度（像素），范围从基础值到最大值</returns>
    private int ResolveGlowThickness(EngineSnapshot snapshot)
    {
        // 获取配置的最大光晕厚度
        var max = _settings.GlowMaxThicknessPixels;
        // 计算基础厚度：最大值的三分之一，但介于12和最大值之间
        var baseThickness = Math.Clamp(max / 3, 12, max);

        /* Hard 警示：工作超过最大阈值后，渗透宽度逐步增长到上限 */
        // 当处于工作状态且工作压力等级为Hard时，根据超时时间增加厚度
        if (snapshot.State == ElasticBreathState.Working && snapshot.WorkingPressure == WorkingPressureLevel.Hard)
        {
            // 计算已超过最大工作阈值的时间
            var overtime = snapshot.WorkingCycleElapsed - _settings.MaxWorkThreshold;
            // 将超时秒数映射到0-1的比例，每120秒达到最大比例
            var ratio = Math.Clamp(overtime.TotalSeconds / 120d, 0, 1);
            // 基础厚度加上按比例增长的额外厚度
            return (int)Math.Round(baseThickness + ((max - baseThickness) * ratio));
        }

        /* 休息超时：渗透宽度逐步增长到上限 */
        // 当处于休息状态且休息压力等级为Overtime时，根据超时时间增加厚度
        if (snapshot.State == ElasticBreathState.Resting && snapshot.RestPressure == RestPressureLevel.Overtime)
        {
            // 计算已超过休息超时阈值的时间
            var overtime = snapshot.RestingCycleElapsed - _settings.RestOvertimeThreshold;
            // 将超时秒数映射到0-1的比例，每120秒达到最大比例
            var ratio = Math.Clamp(overtime.TotalSeconds / 120d, 0, 1);
            // 基础厚度加上按比例增长的额外厚度
            return (int)Math.Round(baseThickness + ((max - baseThickness) * ratio));
        }

        // 非超时状态，返回基础厚度
        return baseThickness;
    }

    /// <summary>
    /// 根据 ElasticBreathState 枚举值，返回对应的本地化状态文本。
    /// </summary>
    /// <param name="state">要解析的状态枚举值。</param>
    /// <returns>本地化文本字符串；若状态未定义，则返回状态名称的字符串表示。</returns>
    private string ResolveStateText(ElasticBreathState state)
        // 使用 switch 表达式根据状态枚举返回相应的本地化文本
        => state switch
        {
            ElasticBreathState.Idle => _localization.T("state.idle"), // 空闲状态
            ElasticBreathState.Working => _localization.T("state.working"), // 工作状态
            ElasticBreathState.Paused => _localization.T("state.paused"), // 暂停状态
            ElasticBreathState.Resting => _localization.T("state.resting"), // 休息状态
            _ => state.ToString() // 兜底处理：对于未定义的状态，返回状态名称
        };

    /// <summary>
    /// 根据弹性呼吸状态获取对应的本地化中心状态显示文本。
    /// </summary>
    /// <param name="state">弹性呼吸状态枚举值。</param>
    /// <returns>对应状态的本地化显示文本。</returns>
    private string ResolveCenterStateText(ElasticBreathState state)
        // 使用 switch 表达式匹配不同的状态枚举值
        => state switch
        {
            // 当状态为空闲时，返回空闲的本地化文本
            ElasticBreathState.Idle => _localization.T("center.state.idle"),
            // 当状态为工作中时，返回工作中的本地化文本
            ElasticBreathState.Working => _localization.T("center.state.working"),
            // 当状态为暂停时，返回暂停的本地化文本
            ElasticBreathState.Paused => _localization.T("center.state.paused"),
            // 当状态为休息中时，返回休息中的本地化文本
            ElasticBreathState.Resting => _localization.T("center.state.resting"),
            // 对于其他未明确处理的状态，将状态名转为大写字符串返回作为后备
            _ => state.ToString().ToUpperInvariant()
        };

    /// <summary>
    /// 根据引擎快照的状态和压力级别，返回对应的本地化压力文本。
    /// </summary>
    /// <param name="snapshot">包含当前状态和压力级别信息的引擎快照。</param>
    /// <returns>对应状态和压力级别的本地化压力文本。</returns>
    private string ResolvePressureText(EngineSnapshot snapshot)
        => snapshot.State switch
        {
            // 当状态为工作且压力为安全级别时，返回安全压力文本
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Safe => _localization.T("pressure.safe"),
            // 当状态为工作且压力为警告级别时，返回警告压力文本
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Warning => _localization.T("pressure.warning"),
            // 当状态为工作且压力为其他级别时，返回困难压力文本
            ElasticBreathState.Working => _localization.T("pressure.hard"),
            // 当状态为休息且压力为基本级别时，返回基本压力文本
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Base => _localization.T("pressure.base"),
            // 当状态为休息且压力为弹性级别时，返回弹性压力文本
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Elastic => _localization.T("pressure.elastic"),
            // 当状态为休息且压力为其他级别时，返回超时压力文本
            ElasticBreathState.Resting => _localization.T("pressure.overtime"),
            // 当状态为暂停时，返回暂停压力文本
            ElasticBreathState.Paused => _localization.T("pressure.paused"),
            // 其他未匹配的状态，默认返回空闲压力文本
            _ => _localization.T("pressure.idle")
        };

    /// <summary>
    /// 根据给定的 PendingTransitionKind 类型，返回对应的本地化过渡文本。
    /// </summary>
    /// <param name="kind">待处理过渡的类型枚举。</param>
    /// <returns>对应过渡类型的本地化文本。</returns>
    private string ResolveTransitionText(PendingTransitionKind kind)
        // 使用 switch 表达式解析过渡类型，返回相应的本地化字符串
        => kind switch
        {
            PendingTransitionKind.IdleToWorking => _localization.T("transition.idle_to_working"), // 从空闲到工作的过渡
            PendingTransitionKind.WorkingToPaused => _localization.T("transition.working_to_paused"), // 从工作到暂停的过渡
            PendingTransitionKind.PausedToWorking => _localization.T("transition.paused_to_working"), // 从暂停到工作的过渡
            PendingTransitionKind.WorkingToResting => _localization.T("transition.working_to_resting"), // 从工作到休息的过渡
            PendingTransitionKind.RestingToWorking => _localization.T("transition.resting_to_working"), // 从休息到工作的过渡
            _ => kind.ToString() // 默认分支：返回枚举值的字符串表示
        };

    /// <summary>
    /// 根据引擎快照状态，解析并返回对应的UI覆盖层状态。
    /// </summary>
    /// <param name="snapshot">包含引擎当前状态的快照对象。</param>
    /// <returns>与快照中状态对应的覆盖层枚举值。</returns>
    private static EdgeOverlayState ResolveOverlayState(EngineSnapshot snapshot)
        => snapshot.State switch
        {
            // 当处于工作状态且工作压力为警告级别时，返回警告状态
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Warning => EdgeOverlayState.Warning,
            // 当处于工作状态且工作压力为高强度级别时，返回高强度状态
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Hard => EdgeOverlayState.Hard,
            // 当处于休息状态且休息压力为基准级别时，返回基准休息状态
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Base => EdgeOverlayState.RestBase,
            // 当处于休息状态且休息压力为弹性级别时，返回弹性休息状态
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Elastic => EdgeOverlayState.RestElastic,
            // 当处于休息状态且休息压力为超时级别时，返回超时休息状态
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Overtime => EdgeOverlayState.RestOvertime,
            // 当处于暂停状态时，返回暂停状态
            ElasticBreathState.Paused => EdgeOverlayState.Paused,
            // 其他所有未匹配的情况，返回隐藏状态
            _ => EdgeOverlayState.Hidden
        };

    /// <summary>
    /// 根据弹性呼吸引擎的快照信息，解析并返回相应的进度条颜色。
    /// </summary>
    /// <param name="snapshot">弹性呼吸引擎的快照，包含状态和压力等级信息。</param>
    /// <returns>一个表示对应状态的颜色值（MediaColor）。</returns>
    private static MediaColor ResolveProgressColor(EngineSnapshot snapshot)
        // 使用 switch 表达式，根据快照中的状态和压力等级组合，匹配并返回对应颜色。
        => snapshot.State switch
        {
            // 工作状态 (Working) 下，根据工作压力等级细分
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Safe => MediaColor.FromRgb(50, 178, 101), // 工作状态 & 压力安全：绿色
            ElasticBreathState.Working when snapshot.WorkingPressure == WorkingPressureLevel.Warning => MediaColor.FromRgb(237, 144, 57), // 工作状态 & 压力警告：橙色
            ElasticBreathState.Working => MediaColor.FromRgb(224, 64, 64), // 工作状态 & 其他压力（如危险）：红色
            // 休息状态 (Resting) 下，根据静息压力等级细分
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Base => MediaColor.FromRgb(54, 179, 102), // 休息状态 & 基础压力：绿色
            ElasticBreathState.Resting when snapshot.RestPressure == RestPressureLevel.Elastic => MediaColor.FromRgb(41, 201, 105), // 休息状态 & 弹性压力：绿色
            ElasticBreathState.Resting => MediaColor.FromRgb(35, 169, 94), // 休息状态 & 其他压力等级：绿色
            // 暂停状态 (Paused)
            ElasticBreathState.Paused => MediaColor.FromRgb(130, 130, 130), // 暂停状态：灰色
            // 默认情况（兜底）
            _ => MediaColor.FromRgb(120, 160, 140) // 未匹配到其他条件：浅绿色
        };

    /// <summary>
    /// 格式化时间跨度为可读字符串。
    /// </summary>
    /// <param name="span">要格式化的时间跨度。</param>
    /// <param name="shortStyle">指示是否使用短格式。短格式为"分:秒"，长格式为"时:分:秒"。</param>
    /// <returns>格式化后的时间字符串。</returns>
    private static string FormatDuration(TimeSpan span, bool shortStyle)
        // 根据 shortStyle 参数选择不同的格式化方式
        => shortStyle
            // 短格式：显示总分钟数和秒数，例如 "05:30"
            ? $"{(int)span.TotalMinutes:00}:{span.Seconds:00}"
            // 长格式：显示总小时数、分钟和秒数，例如 "01:05:30"
            : $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";

    /// <summary>
    /// 根据给定的中心点、半径和进度比例，构建表示进度弧的几何图形。
    /// </summary>
    /// <param name="center">圆弧的中心点。</param>
    /// <param name="radius">圆弧的半径。</param>
    /// <param name="progressRatio">进度比例，范围从0到1。</param>
    /// <returns>表示进度弧的几何图形对象。</returns>
    private static Geometry BuildArcGeometry(System.Windows.Point center, double radius, double progressRatio)
    {
        // 将进度比例限制在0到1之间
        progressRatio = Math.Clamp(progressRatio, 0, 1);

        // 进度比例为0或负数时，返回空几何图形
        if (progressRatio <= 0)
        {
            return Geometry.Empty;
        }

        // 进度比例非常接近1时，使用完整的椭圆几何图形以提高性能和显示平滑度
        if (progressRatio >= 0.9999)
        {
            return new EllipseGeometry(center, radius, radius);
        }

        // 起始角固定为-90度（即12点钟方向）
        var startAngle = -90d;
        // 根据进度比例计算结束角度
        var endAngle = startAngle + (360d * progressRatio);

        // 根据角度计算圆弧的起点和终点坐标
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, endAngle);

        // 判断是否为大弧：当进度比例大于0.5时，弧线超过半圆，需要标记为大弧
        var largeArc = progressRatio > 0.5;

        // 创建路径图形，设置起点，并明确不闭合路径
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        // 添加弧线段，参数依次为：终点、尺寸（x半径，y半径）、旋转角、大弧标志、扫描方向（顺时针）、是否立即绘制
        figure.Segments.Add(new ArcSegment(end, new System.Windows.Size(radius, radius), 0, largeArc, SweepDirection.Clockwise, true));

        // 将路径图形封装为几何图形并返回
        return new PathGeometry(new[] { figure });
    }

    /// <summary>
    /// 计算圆上指定角度对应的点坐标。
    /// </summary>
    /// <param name="center">圆的中心点坐标。</param>
    /// <param name="radius">圆的半径。</param>
    /// <param name="angleDegrees">角度值（以度为单位）。</param>
    /// <returns>圆上对应角度的点坐标。</returns>
    private static System.Windows.Point PointOnCircle(System.Windows.Point center, double radius, double angleDegrees)
    {
        // 将角度转换为弧度
        var rad = angleDegrees * Math.PI / 180d;
        // 使用三角函数计算圆上点的坐标并返回
        return new System.Windows.Point(center.X + (radius * Math.Cos(rad)), center.Y + (radius * Math.Sin(rad)));
    }

    /// <summary>
    /// 处理开始工作按钮的点击事件。根据引擎的当前状态执行相应操作，如暂停、恢复或开始工作。
    /// </summary>
    private void StartWorkButton_Click(object sender, RoutedEventArgs e)
    {
        // 根据引擎的当前状态执行不同的操作
        switch (_engine.Snapshot.State)
        {
            case ElasticBreathState.Working:
                // 当前状态为工作中，执行暂停操作
                _engine.PauseFromWorking();
                break;
            case ElasticBreathState.Paused:
                // 当前状态为暂停中，检查暂停前的状态
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
                // 默认状态或其他状态，直接开始工作
                _engine.StartWorkingManual();
                break;
        }
    }

    /// <summary>
    /// 处理开始休息按钮的点击事件，根据引擎的当前状态决定操作：如果正在休息则暂停，如果已暂停则根据之前状态恢复或开始休息，其他状态则手动开始休息。
    /// </summary>
    private void StartRestButton_Click(object sender, RoutedEventArgs e)
    {
        // 根据引擎快照的状态执行不同操作
        switch (_engine.Snapshot.State)
        {
            // 当状态为休息时，暂停休息
            case ElasticBreathState.Resting:
                _engine.PauseFromResting();
                break;
            // 当状态为暂停时
            case ElasticBreathState.Paused:
                // 检查暂停前的状态
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
            // 对于其他状态，手动开始休息
            default:
                _engine.StartRestingManual();
                break;
        }
    }

    /// <summary>
    /// 处理停止按钮点击事件，将引擎设置为闲置状态。
    /// </summary>
    private void StopButton_Click(object sender, RoutedEventArgs e) => _engine.StopToIdle();

    /// <summary>
    /// 处理推迟按钮点击事件：在预警/硬性区推迟当前工作提醒，进入冷却期。
    /// </summary>
    private void PostponeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.TryPostpone())
        {
            var screen = _displayTargetService.GetTargetScreen(_settings.PreferredDisplay);
            var b = screen.Bounds;
            _toastWindow.ShowMessage(
                _localization.T("notify.postponed"),
                new Rect(b.Left, b.Top, b.Width, b.Height));
        }
    }

    /// <summary>
    /// 处理暂停提醒按钮点击事件，切换提醒的暂停状态。
    /// </summary>
    private void PauseRemindersButton_Click(object sender, RoutedEventArgs e) => _engine.SetRemindersPaused(!_engine.Snapshot.RemindersPaused); // 取反当前暂停状态以切换

    /// <summary>
    /// 处理打开设置按钮点击事件，显示设置窗口并应用设置。
    /// </summary>
    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // 创建设置窗口实例，并设置所有者为当前窗口
        var dialog = new SettingsWindow(_settings, _localization) { Owner = this };
        // 订阅设置应用事件，当设置应用时调用 ApplySettings 方法
        dialog.SettingsApplied += (_, _) => ApplySettings();
        // 显示设置对话框，并忽略返回值
        _ = dialog.ShowDialog();
    }

    /// <summary>
    /// 应用设置，包括保存当前设置、加载本地化资源、更新界面文本、渲染快照和更新覆盖与通知。
    /// </summary>
    private void ApplySettings()
    {
        // 保存当前设置到设置存储
        _settingsStore.Save(_settings);
        // 加载指定语言的本地化资源
        _localization.Load(_settings.Language);
        // 应用本地化更改到界面
        ApplyLocalization();
        // 更新待处理文本显示设置已保存的消息
        PendingText.Text = _localization.T("status.settings_saved");
        // 渲染引擎的当前快照
        RenderSnapshot(_engine.Snapshot);
        // 更新覆盖层和通知基于引擎快照
        UpdateOverlayAndNotifications(_engine.Snapshot);
    }

    /// <summary>
    /// 当帮助按钮被点击时调用此方法，用于打开帮助窗口。
    /// </summary>
    /// <param name="sender">事件的发送者</param>
    /// <param name="e">路由事件参数</param>
    private void OpenHelpButton_Click(object sender, RoutedEventArgs e)
    {
        // 创建帮助窗口实例，设置本地化参数和所有者为当前窗口
        var dialog = new HelpWindow(_localization) { Owner = this };
        // 显示帮助窗口对话框
        _ = dialog.ShowDialog();
    }
}
