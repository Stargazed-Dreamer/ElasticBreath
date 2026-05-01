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

    public BreathEngine(ElasticBreathSettings settings, InputMonitor inputMonitor)
    {
        _settings = settings;
        _inputMonitor = inputMonitor;
        _currentDay = DateOnly.FromDateTime(DateTime.Now);
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTick;
        _lastTickUtc = DateTime.UtcNow;
        Snapshot = BuildSnapshot();
    }

    public event EventHandler<EngineSnapshot>? SnapshotChanged;
    public EngineSnapshot Snapshot { get; private set; }

    public void Start()
    {
        _lastTickUtc = DateTime.UtcNow;
        _timer.Start();
        PublishSnapshot();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    /// <summary>手动开始工作，根据当前状态决定是否重置计时</summary>
    public void StartWorkingManual()
    {
        _sessionLocked = false;
        _remindersPaused = false;
        _pendingTransition = null;
        _idleActivityProbeDuration = TimeSpan.Zero;
        switch (_state)
        {
            case ElasticBreathState.Idle:
            case ElasticBreathState.Paused:
                _workingCycleElapsed = TimeSpan.Zero;
                _restingCycleElapsed = TimeSpan.Zero;
                _state = ElasticBreathState.Working;
                break;
            case ElasticBreathState.Resting:
                /* 休息时长短于最短有效休息时长，认为休息无效，继续之前的工作计时 */
                if (!_restWasEffective && _restingCycleElapsed < _settings.MinEffectiveRestThreshold)
                {
                    _workingCycleElapsed = _workingCycleElapsedBeforeRest + _restingCycleElapsed;
                }
                else
                {
                    _workingCycleElapsed = TimeSpan.Zero;
                }
                _restingCycleElapsed = TimeSpan.Zero;
                _restWasEffective = true;
                _state = ElasticBreathState.Working;
                break;
            case ElasticBreathState.Working:
                break;
        }
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
        PublishSnapshot();
    }

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
        PublishSnapshot();
    }

    /// <summary>暂停/恢复提醒。暂停时切换到 idle，恢复时保持当前状态</summary>
    public void SetRemindersPaused(bool paused)
    {
        _remindersPaused = paused;
        if (paused)
        {
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
        if (_state == ElasticBreathState.Working)
        {
            _workingCycleElapsedBeforeRest = _workingCycleElapsed;
            _restWasEffective = false;
            _restingCycleElapsed = TimeSpan.Zero;
            _state = ElasticBreathState.Resting;
            PublishSnapshot();
            return _state;
        }

        if (_state == ElasticBreathState.Resting)
        {
            /* 角落触发切回工作：检查休息是否有效 */
            if (!_restWasEffective && _restingCycleElapsed < _settings.MinEffectiveRestThreshold)
            {
                _workingCycleElapsed = _workingCycleElapsedBeforeRest + _restingCycleElapsed;
            }
            else
            {
                _workingCycleElapsed = TimeSpan.Zero;
            }
            _restingCycleElapsed = TimeSpan.Zero;
            _restWasEffective = true;
            _state = ElasticBreathState.Working;
            PublishSnapshot();
        }

        return _state;
    }

    /// <summary>取消当前待处理的状态切换</summary>
    public void CancelPendingTransition()
    {
        _pendingTransition = null;
        PublishSnapshot();
    }

    /// <summary>处理系统锁屏/解锁事件，锁屏时切换到 idle</summary>
    public void HandleSessionSwitch(bool isLocked)
    {
        _sessionLocked = isLocked;
        _pendingTransition = null;
        if (isLocked)
        {
            /* 锁屏时直接切换到 idle，避免解锁后状态检测异常 */
            _state = ElasticBreathState.Idle;
            _workingCycleElapsed = TimeSpan.Zero;
            _restingCycleElapsed = TimeSpan.Zero;
            _idleActivityProbeDuration = TimeSpan.Zero;
            _restWasEffective = true;
        }
        PublishSnapshot();
    }

    /// <summary>每秒 Tick：推进计时、处理自动状态切换、处理待处理切换</summary>
    private void OnTick(object? sender, EventArgs e)
    {
        var nowUtc = DateTime.UtcNow;
        var delta = nowUtc - _lastTickUtc;
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
        switch (_state)
        {
            case ElasticBreathState.Working:
                _workingCycleElapsed += delta;
                _totalWorkingToday += delta;
                break;
            case ElasticBreathState.Resting:
                _restingCycleElapsed += delta;
                _totalRestingToday += delta;
                break;
        }
    }

    /// <summary>根据输入采样判断是否应触发自动状态切换，创建待处理切换</summary>
    private void HandleAutomaticTransitions(InputSample sample, TimeSpan delta)
    {
        if (_pendingTransition is not null || _sessionLocked || _remindersPaused)
        {
            return;
        }

        switch (_state)
        {
            case ElasticBreathState.Idle:
                /* 检测到持续活动，累积探测时长 */
                if (sample.IdleDuration <= _settings.SmartDetectGapThreshold)
                {
                    _idleActivityProbeDuration += delta;
                }
                else
                {
                    _idleActivityProbeDuration = TimeSpan.Zero;
                }

                if (_idleActivityProbeDuration >= _settings.IdleToWorkDetectThreshold)
                {
                    SchedulePendingTransition(PendingTransitionKind.IdleToWorking, "notify.idle_to_working");
                    _idleActivityProbeDuration = TimeSpan.Zero;
                }
                break;

            case ElasticBreathState.Working:
                /* 无操作自动转休息：通过待处理切换显示倒计时 */
                if (sample.IdleDuration >= _settings.AutoRestAfterIdleThreshold)
                {
                    SchedulePendingTransition(PendingTransitionKind.WorkingToResting, "notify.working_to_resting");
                }
                break;

            case ElasticBreathState.Paused:
                /* 检测到活动，准备恢复工作 */
                if (sample.IdleDuration <= _settings.SmartDetectGapThreshold)
                {
                    SchedulePendingTransition(PendingTransitionKind.PausedToWorking, "notify.paused_to_working");
                }
                break;

            case ElasticBreathState.Resting:
                /* 休息时检测到持续输入，准备切回工作 */
                if (sample.DenseInputDuration >= _settings.RestToWorkDetectThreshold)
                {
                    SchedulePendingTransition(PendingTransitionKind.RestingToWorking, "notify.resting_to_working");
                }

                /* 休息时离开判定：用户 idle 超过阈值，确认休息有效 */
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
        if (_pendingTransition is null)
        {
            return;
        }

        /* 检查待处理切换的条件是否仍然满足，不满足则取消 */
        switch (_pendingTransition.Kind)
        {
            case PendingTransitionKind.IdleToWorking:
            case PendingTransitionKind.PausedToWorking:
                /* 需要持续活动，如果用户停止操作则取消 */
                if (sample.IdleDuration > _settings.SmartDetectGapThreshold)
                {
                    _pendingTransition = null;
                    return;
                }
                break;

            case PendingTransitionKind.WorkingToPaused:
                /* 需要持续离开，如果用户回来操作则取消 */
                if (sample.IdleDuration < _settings.AwayThreshold)
                {
                    _pendingTransition = null;
                    return;
                }
                break;

            case PendingTransitionKind.WorkingToResting:
                /* 需要持续无操作，如果用户回来操作则取消 */
                if (sample.IdleDuration < _settings.AutoRestAfterIdleThreshold)
                {
                    _pendingTransition = null;
                    return;
                }
                break;

            case PendingTransitionKind.RestingToWorking:
                /* 需要持续输入，如果用户停止操作则取消 */
                if (sample.DenseInputDuration < _settings.RestToWorkDetectThreshold)
                {
                    _pendingTransition = null;
                    return;
                }
                break;
        }

        _pendingTransition.Remaining -= delta;
        if (_pendingTransition.Remaining > TimeSpan.Zero)
        {
            return;
        }

        /* 倒计时结束，执行状态切换 */
        switch (_pendingTransition.Kind)
        {
            case PendingTransitionKind.IdleToWorking:
                _workingCycleElapsed = _settings.IdleToWorkDetectThreshold;
                _state = ElasticBreathState.Working;
                break;
            case PendingTransitionKind.WorkingToPaused:
                _state = ElasticBreathState.Paused;
                break;
            case PendingTransitionKind.PausedToWorking:
                _state = ElasticBreathState.Working;
                break;
            case PendingTransitionKind.WorkingToResting:
                _workingCycleElapsedBeforeRest = _workingCycleElapsed;
                _restWasEffective = false;
                _restingCycleElapsed = _settings.AutoRestAfterIdleThreshold;
                _state = ElasticBreathState.Resting;
                break;
            case PendingTransitionKind.RestingToWorking:
                /* 休息时长短于最短有效休息时长，认为休息无效，继续之前的工作计时 */
                if (!_restWasEffective && _restingCycleElapsed < _settings.MinEffectiveRestThreshold)
                {
                    _workingCycleElapsed = _workingCycleElapsedBeforeRest + _restingCycleElapsed;
                }
                else
                {
                    _workingCycleElapsed = _settings.RestToWorkDetectThreshold;
                }
                _restingCycleElapsed = TimeSpan.Zero;
                _restWasEffective = true;
                _state = ElasticBreathState.Working;
                break;
        }

        _pendingTransition = null;
    }

    /// <summary>根据工作周期已用时间计算工作压力等级</summary>
    private WorkingPressureLevel GetWorkingPressure()
    {
        if (_workingCycleElapsed < _settings.MinWorkThreshold)
        {
            return WorkingPressureLevel.Safe;
        }
        if (_workingCycleElapsed < _settings.MaxWorkThreshold)
        {
            return WorkingPressureLevel.Warning;
        }
        return WorkingPressureLevel.Hard;
    }

    /// <summary>根据休息周期已用时间计算休息压力等级</summary>
    private RestPressureLevel GetRestPressure()
    {
        if (_restingCycleElapsed < _settings.DefaultRestThreshold)
        {
            return RestPressureLevel.Base;
        }
        if (_restingCycleElapsed < _settings.RestOvertimeThreshold)
        {
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
        if (_pendingTransition is null && !_sessionLocked && !_remindersPaused)
        {
            probe = _state switch
            {
                ElasticBreathState.Idle when _idleActivityProbeDuration > TimeSpan.Zero
                    => new DetectionProbeSnapshot("probe.idle_to_working", _idleActivityProbeDuration, _settings.IdleToWorkDetectThreshold),
                ElasticBreathState.Working when _lastIdleDuration > TimeSpan.Zero
                    => new DetectionProbeSnapshot("probe.working_to_resting", _lastIdleDuration, _settings.AutoRestAfterIdleThreshold),
                ElasticBreathState.Resting when _inputMonitor.CurrentDenseInputDuration > TimeSpan.Zero
                    => new DetectionProbeSnapshot("probe.resting_to_working", _inputMonitor.CurrentDenseInputDuration, _settings.RestToWorkDetectThreshold),
                _ => null
            };
        }

        return new EngineSnapshot(
            _state,
            GetWorkingPressure(),
            GetRestPressure(),
            _workingCycleElapsed,
            _restingCycleElapsed,
            _totalWorkingToday,
            _totalRestingToday,
            pending,
            probe,
            _remindersPaused,
            _sessionLocked,
            _stateBeforePause,
            DateTimeOffset.Now);
    }

    /// <summary>发布状态快照，通知所有订阅者</summary>
    private void PublishSnapshot()
    {
        Snapshot = BuildSnapshot();
        SnapshotChanged?.Invoke(this, Snapshot);
    }
}
