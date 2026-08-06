using ElasticBreath.App.Domain;
using ElasticBreath.App.Services;
using Xunit;

namespace ElasticBreath.Tests;

public class BreathEngineTransitionTests : IDisposable
{
    private readonly BreathEngine _engine;
    private readonly ElasticBreathSettings _settings;

    public BreathEngineTransitionTests()
    {
        _settings = new ElasticBreathSettings().Sanitize();
        // Do NOT call Start(): avoids DispatcherTimer ticking and Win32 input sampling.
        _engine = new BreathEngine(_settings, new InputMonitor(new RemoteInputFilterService()));
    }

    public void Dispose() => _engine.Dispose();

    [Fact]
    public void InitialState_IsIdle()
    {
        Assert.Equal(ElasticBreathState.Idle, _engine.Snapshot.State);
    }

    [Fact]
    public void StartWorkingManual_FromIdle_TransitionsToWorking()
    {
        _engine.StartWorkingManual();
        Assert.Equal(ElasticBreathState.Working, _engine.Snapshot.State);
        Assert.Equal(TimeSpan.Zero, _engine.Snapshot.WorkingCycleElapsed);
    }

    [Fact]
    public void StartRestingManual_TransitionsToResting()
    {
        _engine.StartWorkingManual();
        _engine.StartRestingManual();
        Assert.Equal(ElasticBreathState.Resting, _engine.Snapshot.State);
        Assert.Equal(TimeSpan.Zero, _engine.Snapshot.RestingCycleElapsed);
    }

    [Fact]
    public void PauseFromWorking_TransitionsToPaused_WithStateBeforePauseWorking()
    {
        _engine.StartWorkingManual();
        _engine.PauseFromWorking();
        Assert.Equal(ElasticBreathState.Paused, _engine.Snapshot.State);
        Assert.Equal(ElasticBreathState.Working, _engine.Snapshot.StateBeforePause);
    }

    [Fact]
    public void ResumeWorking_FromPaused_TransitionsToWorking()
    {
        _engine.StartWorkingManual();
        _engine.PauseFromWorking();
        _engine.ResumeWorking();
        Assert.Equal(ElasticBreathState.Working, _engine.Snapshot.State);
    }

    [Fact]
    public void StopToIdle_TransitionsToIdle_AndResetsCycleElapsed()
    {
        _engine.StartWorkingManual();
        _engine.StopToIdle();
        Assert.Equal(ElasticBreathState.Idle, _engine.Snapshot.State);
        Assert.Equal(TimeSpan.Zero, _engine.Snapshot.WorkingCycleElapsed);
        Assert.Equal(TimeSpan.Zero, _engine.Snapshot.RestingCycleElapsed);
    }

    [Fact]
    public void TriggerCornerTransition_FromWorking_TransitionsToResting()
    {
        _engine.StartWorkingManual();
        var result = _engine.TriggerCornerTransition();
        Assert.Equal(ElasticBreathState.Resting, result);
        Assert.Equal(ElasticBreathState.Resting, _engine.Snapshot.State);
    }

    [Fact]
    public void TriggerCornerTransition_FromResting_TransitionsToWorking()
    {
        _engine.StartWorkingManual();
        _engine.TriggerCornerTransition(); // Working → Resting
        var result = _engine.TriggerCornerTransition(); // Resting → Working
        Assert.Equal(ElasticBreathState.Working, result);
        Assert.Equal(ElasticBreathState.Working, _engine.Snapshot.State);
    }

    [Fact]
    public void HandleSessionSwitch_Lock_TransitionsToIdle_NotResting()
    {
        _engine.StartWorkingManual();
        _engine.HandleSessionSwitch(isLocked: true);
        Assert.Equal(ElasticBreathState.Idle, _engine.Snapshot.State);
        Assert.NotEqual(ElasticBreathState.Resting, _engine.Snapshot.State);
    }

    [Fact]
    public void TryPostpone_ReturnsFalse_WhenIdle()
    {
        Assert.False(_engine.TryPostpone());
    }

    [Fact]
    public void TryPostpone_ReturnsFalse_WhenWorkingSafe()
    {
        _engine.StartWorkingManual();
        // Without OnTick advancing the working cycle, pressure stays Safe.
        Assert.Equal(WorkingPressureLevel.Safe, _engine.Snapshot.WorkingPressure);
        Assert.False(_engine.TryPostpone());
    }

    [Fact]
    public void SetRemindersPaused_True_TransitionsToIdleAndSetsFlag()
    {
        _engine.StartWorkingManual();
        _engine.SetRemindersPaused(true);
        Assert.Equal(ElasticBreathState.Idle, _engine.Snapshot.State);
        Assert.True(_engine.Snapshot.RemindersPaused);
    }

    [Fact]
    public void Snapshot_Postpone_IsNonNullWithExpectedDailyLimit()
    {
        Assert.NotNull(_engine.Snapshot.Postpone);
        Assert.Equal(_settings.DailyPostponeLimit, _engine.Snapshot.Postpone.DailyLimit);
    }

    [Fact]
    public void CancelPendingTransition_DoesNotThrowAndKeepsState()
    {
        _engine.StartWorkingManual();
        _engine.CancelPendingTransition();
        Assert.Equal(ElasticBreathState.Working, _engine.Snapshot.State);
    }
}
