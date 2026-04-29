using System.Windows.Threading;
using ElasticBreath.App.Domain;

namespace ElasticBreath.App.Services;

public sealed class BreathEngine : IDisposable
{
    private sealed class PendingTransition
    {
        public required PendingTransitionKind Kind { get; init; }
        public required string Message { get; init; }
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
    private DateTime _awayPromptSuppressedUntilUtc = DateTime.MinValue;
    private DateTime _idlePromptSuppressedUntilUtc = DateTime.MinValue;
    private bool _remindersPaused;

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

    public void StartWorkingManual()
    {
        _remindersPaused = false;
        _pendingTransition = null;
        switch (_state)
        {
            case ElasticBreathState.Idle:
            case ElasticBreathState.Resting:
                _workingCycleElapsed = TimeSpan.Zero;
                _restingCycleElapsed = TimeSpan.Zero;
                _state = ElasticBreathState.Working;
                break;
            case ElasticBreathState.Paused:
            case ElasticBreathState.Working:
                _state = ElasticBreathState.Working;
                break;
        }

        PublishSnapshot();
    }

    public void StartRestingManual()
    {
        _remindersPaused = false;
        _pendingTransition = null;
        _restingCycleElapsed = TimeSpan.Zero;
        _state = ElasticBreathState.Resting;
        PublishSnapshot();
    }

    public void StopToIdle()
    {
        _pendingTransition = null;
        _state = ElasticBreathState.Idle;
        _workingCycleElapsed = TimeSpan.Zero;
        _restingCycleElapsed = TimeSpan.Zero;
        PublishSnapshot();
    }

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

    public void TriggerCornerTransition()
    {
        _pendingTransition = null;
        if (_state == ElasticBreathState.Working)
        {
            _restingCycleElapsed = TimeSpan.Zero;
            _state = ElasticBreathState.Resting;
            PublishSnapshot();
            return;
        }

        if (_state == ElasticBreathState.Resting)
        {
            _restingCycleElapsed = TimeSpan.Zero;
            _workingCycleElapsed = TimeSpan.Zero;
            _state = ElasticBreathState.Working;
            PublishSnapshot();
        }
    }

    public void CancelPendingTransition()
    {
        if (_pendingTransition is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (_pendingTransition.Kind == PendingTransitionKind.WorkingToPaused)
        {
            _awayPromptSuppressedUntilUtc = now + _settings.PostponeCooldown;
        }
        else
        {
            _idlePromptSuppressedUntilUtc = now + _settings.PostponeCooldown;
        }

        _pendingTransition = null;
        PublishSnapshot();
    }

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

        var sample = _inputMonitor.Sample(TimeSpan.FromSeconds(1));
        AdvanceCycleTime(delta);
        HandleAutomaticTransitions(sample, nowUtc);
        HandlePendingTransition(delta);
        PublishSnapshot();
    }

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

    private void HandleAutomaticTransitions(InputSample sample, DateTime nowUtc)
    {
        if (_pendingTransition is not null)
        {
            return;
        }

        switch (_state)
        {
            case ElasticBreathState.Idle:
                if (!_remindersPaused && sample.HadActivity && nowUtc >= _idlePromptSuppressedUntilUtc)
                {
                    SchedulePendingTransition(
                        PendingTransitionKind.IdleToWorking,
                        "Activity detected. Start working in {0}s. Click to cancel.");
                }
                break;

            case ElasticBreathState.Working:
                if (sample.IdleDuration >= _settings.AwayThreshold && nowUtc >= _awayPromptSuppressedUntilUtc)
                {
                    SchedulePendingTransition(
                        PendingTransitionKind.WorkingToPaused,
                        "Away detected. Pause working in {0}s. Click to cancel.");
                }
                break;

            case ElasticBreathState.Paused:
                if (sample.HadActivity && nowUtc >= _idlePromptSuppressedUntilUtc)
                {
                    SchedulePendingTransition(
                        PendingTransitionKind.PausedToWorking,
                        "Welcome back. Resume working in {0}s. Click to stay paused.");
                }
                break;

            case ElasticBreathState.Resting:
                if (sample.DenseInputDuration >= TimeSpan.FromSeconds(30))
                {
                    _restingCycleElapsed = TimeSpan.Zero;
                    _workingCycleElapsed = TimeSpan.Zero;
                    _state = ElasticBreathState.Working;
                }
                break;
        }
    }

    private void SchedulePendingTransition(PendingTransitionKind kind, string messageTemplate)
    {
        var seconds = _settings.AutoTransitionCountdownSeconds;
        _pendingTransition = new PendingTransition
        {
            Kind = kind,
            Message = string.Format(messageTemplate, seconds),
            Remaining = TimeSpan.FromSeconds(seconds)
        };
    }

    private void HandlePendingTransition(TimeSpan delta)
    {
        if (_pendingTransition is null)
        {
            return;
        }

        _pendingTransition.Remaining -= delta;
        if (_pendingTransition.Remaining > TimeSpan.Zero)
        {
            return;
        }

        switch (_pendingTransition.Kind)
        {
            case PendingTransitionKind.IdleToWorking:
                _workingCycleElapsed = TimeSpan.Zero;
                _state = ElasticBreathState.Working;
                break;
            case PendingTransitionKind.WorkingToPaused:
                _state = ElasticBreathState.Paused;
                break;
            case PendingTransitionKind.PausedToWorking:
                _state = ElasticBreathState.Working;
                break;
        }

        _pendingTransition = null;
    }

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

    private EngineSnapshot BuildSnapshot()
    {
        var pending = _pendingTransition is null
            ? null
            : new PendingTransitionSnapshot(_pendingTransition.Kind, _pendingTransition.Message, _pendingTransition.Remaining);

        return new EngineSnapshot(
            _state,
            GetWorkingPressure(),
            GetRestPressure(),
            _workingCycleElapsed,
            _restingCycleElapsed,
            _totalWorkingToday,
            _totalRestingToday,
            pending,
            _remindersPaused,
            DateTimeOffset.Now);
    }

    private void PublishSnapshot()
    {
        Snapshot = BuildSnapshot();
        SnapshotChanged?.Invoke(this, Snapshot);
    }
}
