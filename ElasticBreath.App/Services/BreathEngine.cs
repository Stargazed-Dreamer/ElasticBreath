using System.Windows.Threading;
using ElasticBreath.App.Domain;

namespace ElasticBreath.App.Services;

/// <summary>
/// 核心状态机引擎，驱动工作/休息/暂停/空闲的状态切换与计时。
/// 每秒触发一次 Tick，根据输入采样决定自动状态切换。
/// </summary>
public sealed class BreathEngine : IDisposable
{
    /// <summary>待处理的状态切换，包含切换类型、提示消息键和剩余倒计时</summary>
    private sealed class PendingTransition
    {
        public required PendingTransitionKind Kind { get; init; }
        public required string MessageKey { get; init; }
        public required TimeSpan Remaining { get; set; }
    }

    private readonly ElasticBreathSettings _settings;
    private readonly InputMonitor _inputMonitor;
    private readonly DispatcherTimer _timer;

    private DateTime _lastTickUtc;
    private DateOnly _currentDay;
    private ElasticBreathState _state = ElasticBreathState.Idle;
    private TimeSpan _workingCycleElapsed = TimeSpan.Zero;
    private TimeSpan _restingCycleElapsed = TimeSpan.Zero;
    private TimeSpan _totalWorkingToday = TimeSpan.Zero;
    private TimeSpan _totalRestingToday = TimeSpan.Zero;
    private PendingTransition? _pendingTransition;
    private bool _remindersPaused;
    private bool _sessionLocked;

    /* 空闲状态下累积的活动探测时长，达到阈值后触发 IdleToWorking */
    private TimeSpan _idleActivityProbeDuration = TimeSpan.Zero;

    /* 最近一次采样的系统空闲时长，用于 BuildSnapshot 中构建探测进度 */
    private TimeSpan _lastIdleDuration = TimeSpan.Zero;

    /* 暂停前所处的状态，用于 UI 决定哪个按钮显示"继续" */
    private ElasticBreathState _stateBeforePause = ElasticBreathState.Idle;

    /* 进入休息前的工作计时，用于判断休息是否有效 */
    private TimeSpan _workingCycleElapsedBeforeRest = TimeSpan.Zero;
    private bool _restWasEffective = true;

/// <summary>
/// 初始化BreathEngine类的新实例，设置基本参数并启动计时器。
/// </summary>
/// <param name="settings">弹性呼吸设置参数</param>
/// <param name="inputMonitor">输入监控器实例，用于检测用户交互</param>
    public BreathEngine(ElasticBreathSettings settings, InputMonitor inputMonitor)
    {
        // 存储传入的呼吸设置参数
        _settings = settings;
        // 存储输入监控器的引用，用于后续事件监听
        _inputMonitor = inputMonitor;
        // 将当前日期设置为今天（使用DateOnly类型仅包含日期信息）
        _currentDay = DateOnly.FromDateTime(DateTime.Now);
        // 创建并初始化一个DispatcherTimer计时器，用于周期性触发呼吸节律
        _timer = new DispatcherTimer
        {
            // 设置计时器触发间隔为1秒
            Interval = TimeSpan.FromSeconds(1)
        };
        // 为计时器的Tick事件绑定事件处理方法OnTick
        _timer.Tick += OnTick;
        // 记录上一次计时器触发的UTC时间，用于计算时间差
        _lastTickUtc = DateTime.UtcNow;
        // 调用BuildSnapshot方法构建初始呼吸状态快照
        Snapshot = BuildSnapshot();
    }

    public event EventHandler<EngineSnapshot>? SnapshotChanged;
    public EngineSnapshot Snapshot { get; private set; }

    /// <summary>
    /// 启动相关功能的入口方法
    /// </summary>
    public void Start()
    {
        // 记录当前UTC时间，用于后续时间间隔计算
        _lastTickUtc = DateTime.UtcNow;
        // 启动内部计时器，开始周期性任务调度
        _timer.Start();
        // 发布初始状态快照，确保数据同步
        PublishSnapshot();
    }

    /// <summary>
    /// 释放定时器资源的方法。
    /// 停止定时器并取消事件订阅，防止内存泄漏。
    /// </summary>
    public void Dispose()
    {
        // 停止定时器，防止继续触发Tick事件
        _timer.Stop();

        // 取消订阅Tick事件，移除OnTick事件处理器，避免内存泄漏
        _timer.Tick -= OnTick;
    }

    /// <summary>手动开始工作，根据当前状态决定是否重置计时</summary>
    public void StartWorkingManual()
    {
        _sessionLocked = false;
        _remindersPaused = false;
        _pendingTransition = null;
        _idleActivityProbeDuration = TimeSpan.Zero;
        /// <summary>
        /// 根据当前弹性呼吸状态执行相应的状态转换逻辑。
        /// 该方法负责管理状态机中的状态流转，处理空闲、暂停、休息和工作状态之间的切换，并重置相关计时器。
        /// </summary>
        /// <param name="_state">当前状态，将根据此状态进行后续处理。</param>
        switch (_state)
        {
            // 处理空闲或暂停状态
            case ElasticBreathState.Idle:
            case ElasticBreathState.Paused:
                // 重置工作周期和休息周期的计时器
                _workingCycleElapsed = TimeSpan.Zero;
                _restingCycleElapsed = TimeSpan.Zero;
                // 转换到工作状态
                _state = ElasticBreathState.Working;
                break;
            // 处理休息状态
            case ElasticBreathState.Resting:
                /* 休息时长短于最短有效休息时长，认为休息无效，继续之前的工作计时 */
                // 判断休息是否无效：即未标记为有效休息，且休息时长未达到最低有效阈值
                // 如果休息无效且休息时间小于最小有效休息阈值
                if (!_restWasEffective && _restingCycleElapsed < _settings.MinEffectiveRestThreshold)
                {
                    // 若休息无效，将休息前已工作的时间加上本次休息时间，作为新的工作周期已用时间
                    _workingCycleElapsed = _workingCycleElapsedBeforeRest + _restingCycleElapsed;
                }
                else
                {
                    // 若休息有效，则重置工作周期计时器
                    _workingCycleElapsed = TimeSpan.Zero;
                }
                // 无论休息是否有效，都重置休息周期计时器
                _restingCycleElapsed = TimeSpan.Zero;
                // 标记当前休息周期为有效
                _restWasEffective = true;
                // 转换到工作状态
                _state = ElasticBreathState.Working;
                break;
            // 处理工作状态，不进行任何操作
            case ElasticBreathState.Working:
                break;
        }
// 调用PublishSnapshot方法以发布当前状态的快照
        PublishSnapshot();
    }

    /// <summary>手动开始休息，保存当前工作计时以备休息有效性判断</summary>
    public void StartRestingManual()
    {
        _sessionLocked = false;
        _remindersPaused = false;
        _pendingTransition = null;
        _workingCycleElapsedBeforeRest = _workingCycleElapsed;
        _restWasEffective = false;
        _restingCycleElapsed = TimeSpan.Zero;
        _state = ElasticBreathState.Resting;
// 调用PublishSnapshot方法，发布当前快照
        PublishSnapshot();
    }

    /// <summary>
    /// 从工作状态暂停
    /// </summary>
    /* 从工作状态暂停 */
    public void PauseFromWorking()
    {
        _pendingTransition = null;
        _stateBeforePause = ElasticBreathState.Working;
        _state = ElasticBreathState.Paused;
        PublishSnapshot();
    }

    /* 从休息状态暂停 */
    public void PauseFromResting()
    {
        _pendingTransition = null;
        /* 从休息暂停时认为休息有效（不管实际时长） */
        _restWasEffective = true;
        _stateBeforePause = ElasticBreathState.Resting;
        _state = ElasticBreathState.Paused;
        PublishSnapshot();
    }

    /* 继续工作（从暂停恢复） */
    public void ResumeWorking()
    {
        _pendingTransition = null;
        _idleActivityProbeDuration = TimeSpan.Zero;
        _state = ElasticBreathState.Working;
        PublishSnapshot();
    }

    /* 继续休息（从暂停恢复） */
    public void ResumeResting()
    {
        _pendingTransition = null;
        _state = ElasticBreathState.Resting;
        PublishSnapshot();
    }

    /// <summary>停止并回到空闲状态，重置所有周期计时</summary>
    public void StopToIdle()
    {
        _pendingTransition = null;
        /* 切到 idle 时认为休息有效（不管实际时长） */
        _restWasEffective = true;
        _state = ElasticBreathState.Idle;
        _workingCycleElapsed = TimeSpan.Zero;
        _restingCycleElapsed = TimeSpan.Zero;
        _idleActivityProbeDuration = TimeSpan.Zero;
// 调用PublishSnapshot方法以发布快照
        PublishSnapshot();
    }

    /// <summary>暂停/恢复提醒。暂停时切换到 idle，恢复时保持当前状态</summary>
    public void SetRemindersPaused(bool paused)
    {
        _remindersPaused = paused;
// 如果处于暂停状态，则停止当前操作并转为空闲状态
// 如果处于暂停状态
        if (paused)
        {
            // 停止当前动作并切换到空闲状态
            StopToIdle();
        }
        else
        {
            PublishSnapshot();
        }
    }

    /// <summary>角落悬停触发状态切换：工作→休息 或 休息→工作</summary>
    public ElasticBreathState TriggerCornerTransition()
    {
        _pendingTransition = null;
        // 如果当前状态为工作状态，则执行休息状态转换
// 检查状态是否为工作状态，如果是，则执行切换到休息状态的逻辑
        if (_state == ElasticBreathState.Working)
        {
            _workingCycleElapsedBeforeRest = _workingCycleElapsed; // 保存进入休息前的工作周期时间
            _restWasEffective = false; // 初始化休息是否有效标志为false
            _restingCycleElapsed = TimeSpan.Zero; // 重置休息周期时间为零
            _state = ElasticBreathState.Resting; // 将状态更改为休息状态
            PublishSnapshot(); // 发布当前状态快照
            return _state; // 返回更新后的状态
        }

        /// <summary>
        /// 处理弹性呼吸状态机中的状态转换逻辑。
        /// 当当前状态为休息时，检查休息的有效性，并相应地更新工作周期的累计时间，
        /// 然后将状态切换为工作。
        /// </summary>
        if (_state == ElasticBreathState.Resting)
        {
            // 如果休息无效（即未达到最小有效休息时长阈值）
            /* 角落触发切回工作：检查休息是否有效 */
            if (!_restWasEffective && _restingCycleElapsed < _settings.MinEffectiveRestThreshold)
            {
                // 将工作周期时间重置为休息前的工作时间加上本次休息时间
                _workingCycleElapsed = _workingCycleElapsedBeforeRest + _restingCycleElapsed;
            }
            else
            {
                // 休息有效，重置工作周期时间为零
                _workingCycleElapsed = TimeSpan.Zero;
            }
            // 重置当前休息周期的累计时间
            _restingCycleElapsed = TimeSpan.Zero;
            // 标记本次休息为有效
            _restWasEffective = true;
            // 切换状态到工作
            _state = ElasticBreathState.Working;
            // 发布新的状态快照
            PublishSnapshot();
        }

        return _state;
    }

    /// <summary>取消当前待处理的状态切换</summary>
    public void CancelPendingTransition()
    {
        _pendingTransition = null;
/// <summary>
    /// 用于触发系统快照的发布。
    /// </summary>
        PublishSnapshot();
    }

    /// <summary>处理系统锁屏/解锁事件，锁屏时切换到 idle</summary>
    public void HandleSessionSwitch(bool isLocked)
    {
        _sessionLocked = isLocked;
        _pendingTransition = null;
/// <summary>
        /// 当锁屏时，将系统状态切换为闲置，并重置所有相关计时器，以确保解锁后状态检测正常。
        /// </summary>
        if (isLocked)
        {
            /* 锁屏时直接切换到 idle，避免解锁后状态检测异常 */
            // 设置状态为闲置模式
            _state = ElasticBreathState.Idle;
            // 将工作周期经过时间重置为零
            _workingCycleElapsed = TimeSpan.Zero;
            // 将休息周期经过时间重置为零
            _restingCycleElapsed = TimeSpan.Zero;
            // 将闲置活动探测持续时间重置为零
            _idleActivityProbeDuration = TimeSpan.Zero;
            // 标记休息有效，以准备后续状态检测
            _restWasEffective = true;
        }
/// <summary>
        /// 发布系统状态快照。
        /// 此方法负责将当前系统的状态信息（快照）发布到指定的目标或存储中。
        /// </summary>
        PublishSnapshot();
    }

    /// <summary>每秒 Tick：推进计时、处理自动状态切换、处理待处理切换</summary>
    private void OnTick(object? sender, EventArgs e)
    {
        var nowUtc = DateTime.UtcNow;
        var delta = nowUtc - _lastTickUtc;
// 如果delta小于或等于零时间间隔，则提前返回
/// <summary>
/// 检查时间差是否为负值或零，如果是则直接返回。
/// </summary>
        // 如果时间差小于或等于零，则直接返回，不进行任何处理
        if (delta <= TimeSpan.Zero)
        {
            return;
        }

        _lastTickUtc = nowUtc;
        ResetDailyCountersIfNeeded();

        var sample = _inputMonitor.Sample(_settings.SmartDetectGapThreshold);
        _lastIdleDuration = sample.IdleDuration;
        AdvanceCycleTime(delta);
        HandleAutomaticTransitions(sample, delta);
        HandlePendingTransition(sample, delta);
        PublishSnapshot();
    }

    /// <summary>跨日时重置今日累计计时</summary>
    private void ResetDailyCountersIfNeeded()
    {
        var day = DateOnly.FromDateTime(DateTime.Now);
// 检查传入的day是否与当前存储的_currentDay相同，如果相同则提前结束方法执行
        if (day == _currentDay)
        {
            return;
        }

        _currentDay = day;
        _totalWorkingToday = TimeSpan.Zero;
        _totalRestingToday = TimeSpan.Zero;
    }

    /// <summary>根据当前状态推进周期计时和今日累计</summary>
    private void AdvanceCycleTime(TimeSpan delta)
    {
        /// <summary>
        /// 根据当前弹性呼吸状态，更新相应的周期计时器和今日累计时间。
        /// </summary>
        switch (_state)
        {
            // 状态：工作周期
            case ElasticBreathState.Working:
                // 累加当前工作周期已用时间
                _workingCycleElapsed += delta;
                // 累加今日工作总时间
                _totalWorkingToday += delta;
                break;
            // 状态：休息周期
            case ElasticBreathState.Resting:
                // 累加当前休息周期已用时间
                _restingCycleElapsed += delta;
                // 累加今日休息总时间
                _totalRestingToday += delta;
                break;
        }
    }

    /// <summary>根据输入采样判断是否应触发自动状态切换，创建待处理切换</summary>
    private void HandleAutomaticTransitions(InputSample sample, TimeSpan delta)
    {
        // 如果有待处理的转换、会话被锁定或提醒被暂停，则直接返回，不执行后续操作
        /// <summary>
        /// 检查是否满足以下任一条件：有待处理事务、会话已锁定或提醒已暂停。
        /// 若满足任一条件，则提前退出当前方法。
        /// </summary>
        if (_pendingTransition is not null || _sessionLocked || _remindersPaused) // 检查：有待处理事务，或会话已锁定，或提醒已暂停
        {
            return;
        }

/// <summary>
/// 根据当前状态处理样本数据和时间增量，实现弹性呼吸状态机的切换逻辑。
/// </summary>
        switch (_state)
        {
            case ElasticBreathState.Idle:
                /* 检测到持续活动，累积探测时长 */
                // 如果空闲时间在智能检测间隔阈值内，说明有持续活动，累加探测时长
                if (sample.IdleDuration <= _settings.SmartDetectGapThreshold)
                {
                    _idleActivityProbeDuration += delta;
                }
                else
                {
                    // 否则重置探测时长，表示活动间断
                    _idleActivityProbeDuration = TimeSpan.Zero;
                }

                // 当累积的探测时长达到从空闲到工作的检测阈值时，调度状态切换
                if (_idleActivityProbeDuration >= _settings.IdleToWorkDetectThreshold)
                {
                    SchedulePendingTransition(PendingTransitionKind.IdleToWorking, "notify.idle_to_working");
                    _idleActivityProbeDuration = TimeSpan.Zero;
                }
                break;

            case ElasticBreathState.Working:
                /* 无操作自动转休息：通过待处理切换显示倒计时 */
                // 如果空闲时间达到自动休息阈值，调度从工作到休息的切换
                if (sample.IdleDuration >= _settings.AutoRestAfterIdleThreshold)
                {
                    SchedulePendingTransition(PendingTransitionKind.WorkingToResting, "notify.working_to_resting");
                }
                break;

            case ElasticBreathState.Paused:
                /* 检测到活动，准备恢复工作 */
                // 如果空闲时间在智能检测间隔阈值内，说明用户恢复活动，调度从暂停到工作的切换
                if (sample.IdleDuration <= _settings.SmartDetectGapThreshold)
                {
                    SchedulePendingTransition(PendingTransitionKind.PausedToWorking, "notify.paused_to_working");
                }
                break;

            case ElasticBreathState.Resting:
                /* 休息时检测到持续输入，准备切回工作 */
                // 如果密集输入持续时间达到从休息到工作的检测阈值，调度状态切换
                if (sample.DenseInputDuration >= _settings.RestToWorkDetectThreshold)
                {
                    SchedulePendingTransition(PendingTransitionKind.RestingToWorking, "notify.resting_to_working");
                }

                /* 休息时离开判定：用户 idle 超过阈值，确认休息有效 */
                // 如果空闲时间达到离开阈值，标记本次休息为有效
                if (sample.IdleDuration >= _settings.AwayThreshold)
                {
                    _restWasEffective = true;
                }
                break;
        }
    }

    /// <summary>创建待处理切换，设置倒计时</summary>
    private void SchedulePendingTransition(PendingTransitionKind kind, string messageKey)
    {
        var seconds = _settings.AutoTransitionCountdownSeconds;
        _pendingTransition = new PendingTransition
        {
            Kind = kind,
            MessageKey = messageKey,
            Remaining = TimeSpan.FromSeconds(seconds)
        };
    }

    /// <summary>处理待处理切换：检查条件、递减倒计时、到期执行切换</summary>
    private void HandlePendingTransition(InputSample sample, TimeSpan delta)
    {
/// <summary>
/// 此代码段检查_pendingTransition变量是否为null，如果是，则提前返回。
/// </summary>
        // 检查_pendingTransition是否为null
        /// <summary>
        /// 检查待处理的过渡是否为空，如果为空则直接返回。
        /// </summary>
        if (_pendingTransition is null)
        {
            return; // 如果为null，则直接返回
        }

        /* 检查待处理切换的条件是否仍然满足，不满足则取消 */
/// <summary>
/// 根据待定转换的类型，检查用户活动条件，如果不满足则取消转换。
/// </summary>
        switch (_pendingTransition.Kind)
        {
            case PendingTransitionKind.IdleToWorking:
            case PendingTransitionKind.PausedToWorking:
                /* 需要持续活动，如果用户停止操作则取消 */
                // 检查用户空闲时间是否超过阈值，如果是则取消转换
// 如果样本的空闲持续时间超过设置的智能检测间隔阈值
                if (sample.IdleDuration > _settings.SmartDetectGapThreshold)
                {
                    _pendingTransition = null; // 清空待定转换
                    return; // 提前返回
                }
                break;

            case PendingTransitionKind.WorkingToPaused:
                /* 需要持续离开，如果用户回来操作则取消 */
                // 检查用户空闲时间是否低于阈值，如果是则取消转换
                /// <summary>
                /// 检查空闲时长是否低于离开阈值，以决定是否重置挂起的转换状态。
                /// </summary>
                // 如果空闲时长未达到系统设置的离开阈值
                if (sample.IdleDuration < _settings.AwayThreshold)
                {
                    // 重置挂起的转换状态并提前返回
                    _pendingTransition = null;
                    return;
                }
                break;

            case PendingTransitionKind.WorkingToResting:
                /* 需要持续无操作，如果用户回来操作则取消 */
                // 检查用户空闲时间是否低于阈值，如果是则取消转换
                /// <summary>
                /// 检查样本是否处于空闲状态，如果空闲时间未达到自动休息阈值则重置待处理状态并提前返回。
                /// </summary>
                if (sample.IdleDuration < _settings.AutoRestAfterIdleThreshold) // 如果样本的当前空闲持续时间小于设置中的自动休息空闲阈值
                {
                    _pendingTransition = null; // 将待处理的状态转换请求置空
                    return; // 直接返回，不执行后续的状态转换逻辑
                }
                break;

            case PendingTransitionKind.RestingToWorking:
                /* 需要持续输入，如果用户停止操作则取消 */
                // 检查密集输入时间是否低于阈值，如果是则取消转换
                /// <summary>
                /// 处理待定的过渡状态。
                /// </summary>
                if (sample.DenseInputDuration < _settings.RestToWorkDetectThreshold) // 如果密集输入持续时间短于从休息到工作的检测阈值
                {
                    _pendingTransition = null; // 重置待定的过渡状态
                    return; // 提前返回，不进行后续处理
                }
                break;
        }

        _pendingTransition.Remaining -= delta;
/// <summary>
/// 检查待处理过渡动画的剩余时间。
/// 如果剩余时间大于零，则提前返回当前方法，不执行后续逻辑。
/// </summary>
        // 如果过渡动画还有剩余时间，则直接返回
// 如果待转换的剩余时间大于零，则直接返回
        if (_pendingTransition.Remaining > TimeSpan.Zero)
        {
            return;
        }

        /* 倒计时结束，执行状态切换 */
/// <summary>
/// 根据待转换的类型执行状态切换。
/// </summary>
        switch (_pendingTransition.Kind)
        {
            case PendingTransitionKind.IdleToWorking:
                // 从空闲切换到工作状态
                _workingCycleElapsed = _settings.IdleToWorkDetectThreshold; // 设置工作周期为“空闲转工作”的检测阈值
                _state = ElasticBreathState.Working; // 设置当前状态为“工作”
                break;
            case PendingTransitionKind.WorkingToPaused:
                // 从工作切换到暂停状态
                _state = ElasticBreathState.Paused; // 设置当前状态为“暂停”
                break;
            case PendingTransitionKind.PausedToWorking:
                // 从暂停切换回工作状态
                _state = ElasticBreathState.Working; // 设置当前状态为“工作”
                break;
            case PendingTransitionKind.WorkingToResting:
                // 从工作切换到休息状态
                _workingCycleElapsedBeforeRest = _workingCycleElapsed; // 保存休息前的工作周期时长
                _restWasEffective = false; // 标记本次休息开始时默认为无效
                _restingCycleElapsed = _settings.AutoRestAfterIdleThreshold; // 设置休息周期为自动休息阈值
                _state = ElasticBreathState.Resting; // 设置当前状态为“休息”
                break;
            case PendingTransitionKind.RestingToWorking:
                /* 休息时长短于最短有效休息时长，认为休息无效，继续之前的工作计时 */
// 如果休息无效且休息时间未达到最小有效阈值
                if (!_restWasEffective && _restingCycleElapsed < _settings.MinEffectiveRestThreshold)
                {
                    // 如果休息被认为无效，则累计之前的工作时长
                    _workingCycleElapsed = _workingCycleElapsedBeforeRest + _restingCycleElapsed;
                }
                else
                {
                    // 如果休息被认为有效，则将工作计时重置为“休息转工作”的检测阈值
                    _workingCycleElapsed = _settings.RestToWorkDetectThreshold;
                }
                _restingCycleElapsed = TimeSpan.Zero; // 重置休息周期计时
                _restWasEffective = true; // 标记本次休息为有效（为后续逻辑准备）
                _state = ElasticBreathState.Working; // 设置当前状态为“工作”
                break;
        }

        _pendingTransition = null;
    }

    /// <summary>根据工作周期已用时间计算工作压力等级</summary>
    private WorkingPressureLevel GetWorkingPressure()
    {
        /// <summary>
        /// 检查当前工作循环耗时是否低于最低工作阈值，若低于则判定为安全压力等级。
        /// </summary>
        if (_workingCycleElapsed < _settings.MinWorkThreshold)
        {
            // 如果工作循环耗时小于设置的最小工作阈值，则当前处于安全状态
            return WorkingPressureLevel.Safe;
        }
// 如果工作周期耗时小于设置的最大工作阈值，则返回警告级别
        if (_workingCycleElapsed < _settings.MaxWorkThreshold)
        {
            return WorkingPressureLevel.Warning;
        }
        return WorkingPressureLevel.Hard;
    }

    /// <summary>根据休息周期已用时间计算休息压力等级</summary>
    private RestPressureLevel GetRestPressure()
    {
// 如果已经过去的休息周期时间小于默认的休息阈值，则返回基础休息压力水平。
        /// <summary>
        /// 判断当前休息循环的已用时间是否低于默认的休息阈值。
        /// 如果条件满足，则返回基础休息压力等级。
        /// </summary>
        if (_restingCycleElapsed < _settings.DefaultRestThreshold)
        {
            return RestPressureLevel.Base;
        }
// 检查休息周期是否未超过设置的加班阈值
/// <summary>
        /// 这个 if 方法检查休息周期是否超过加班阈值，如果未超过则返回弹性压力等级。
        /// </summary>
        if (_restingCycleElapsed < _settings.RestOvertimeThreshold)
        {
            // 如果未超过阈值，则返回弹性压力等级
            return RestPressureLevel.Elastic;
        }
        return RestPressureLevel.Overtime;
    }

    /// <summary>构建当前引擎状态的快照，供 UI 消费</summary>
    private EngineSnapshot BuildSnapshot()
    {
        var pending = _pendingTransition is null
            ? null
            : new PendingTransitionSnapshot(_pendingTransition.Kind, _pendingTransition.MessageKey, _pendingTransition.Remaining);

        /* 构建智能检测探测进度，仅在无待处理切换时显示（避免信息冲突） */
        DetectionProbeSnapshot? probe = null;
/// <summary>
        /// 当没有未决过渡、会话未锁定且提醒未暂停时，根据当前状态创建检测探测快照。
        /// </summary>
        if (_pendingTransition is null && !_sessionLocked && !_remindersPaused) // 条件检查：确保无过渡、会话可用且提醒活跃
        {
            probe = _state switch // 使用switch表达式基于当前状态赋值probe
            {
                ElasticBreathState.Idle when _idleActivityProbeDuration > TimeSpan.Zero // 空闲状态且空闲活动探测持续时间大于零
                    => new DetectionProbeSnapshot("probe.idle_to_working", _idleActivityProbeDuration, _settings.IdleToWorkDetectThreshold), // 创建从空闲到工作的探测快照
                ElasticBreathState.Working when _lastIdleDuration > TimeSpan.Zero // 工作状态且最后空闲持续时间大于零
                    => new DetectionProbeSnapshot("probe.working_to_resting", _lastIdleDuration, _settings.AutoRestAfterIdleThreshold), // 创建从工作到休息的探测快照
                ElasticBreathState.Resting when _inputMonitor.CurrentDenseInputDuration > TimeSpan.Zero // 休息状态且当前密集输入持续时间大于零
                    => new DetectionProbeSnapshot("probe.resting_to_working", _inputMonitor.CurrentDenseInputDuration, _settings.RestToWorkDetectThreshold), // 创建从休息到工作的探测快照
                _ => null // 默认情况，不创建探测快照
            };
        }

// 创建并返回新的引擎快照对象
        return new EngineSnapshot(
            _state,  // 引擎当前状态
            GetWorkingPressure(),  // 获取工作压力
            GetRestPressure(),  // 获取休息压力
            _workingCycleElapsed,  // 工作周期已用时间
            _restingCycleElapsed,  // 休息周期已用时间
            _totalWorkingToday,  // 今日总工作时间
            _totalRestingToday,  // 今日总休息时间
            pending,  // 挂起操作
            probe,  // 探测信息
            _remindersPaused,  // 提醒是否暂停
            _sessionLocked,  // 会话是否锁定
            _stateBeforePause,  // 暂停前状态
            DateTimeOffset.Now);  // 当前时间戳
    }

    /// <summary>发布状态快照，通知所有订阅者</summary>
    private void PublishSnapshot()
    {
        Snapshot = BuildSnapshot();
        SnapshotChanged?.Invoke(this, Snapshot);
    }
}
