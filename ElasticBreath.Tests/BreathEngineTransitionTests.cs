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
    public void SetRemindersPaused_True_TransitionsToPaused_AndRemembersStateBeforePause()
    {
        _engine.StartWorkingManual();
        _engine.SetRemindersPaused(true);
        Assert.Equal(ElasticBreathState.Paused, _engine.Snapshot.State);
        Assert.Equal(ElasticBreathState.Working, _engine.Snapshot.StateBeforePause);
        Assert.True(_engine.Snapshot.RemindersPaused);
    }

    [Fact]
    public void SetRemindersPaused_False_ResumesToStateBeforePause()
    {
        _engine.StartWorkingManual();
        _engine.SetRemindersPaused(true);
        _engine.SetRemindersPaused(false);
        Assert.Equal(ElasticBreathState.Working, _engine.Snapshot.State);
        Assert.False(_engine.Snapshot.RemindersPaused);
    }

    [Fact]
    public void SetRemindersPaused_FromResting_ResumesToResting()
    {
        _engine.StartWorkingManual();
        _engine.StartRestingManual();
        _engine.SetRemindersPaused(true);
        Assert.Equal(ElasticBreathState.Paused, _engine.Snapshot.State);
        Assert.Equal(ElasticBreathState.Resting, _engine.Snapshot.StateBeforePause);
        _engine.SetRemindersPaused(false);
        Assert.Equal(ElasticBreathState.Resting, _engine.Snapshot.State);
    }

    [Fact]
    public void SetRemindersPaused_FromIdle_StaysPausedAndResumesToIdle()
    {
        _engine.SetRemindersPaused(true);
        Assert.Equal(ElasticBreathState.Paused, _engine.Snapshot.State);
        Assert.Equal(ElasticBreathState.Idle, _engine.Snapshot.StateBeforePause);
        _engine.SetRemindersPaused(false);
        Assert.Equal(ElasticBreathState.Idle, _engine.Snapshot.State);
    }

    [Fact]
    public void CancelPendingTransition_DoesNotThrowAndKeepsState()
    {
        _engine.StartWorkingManual();
        _engine.CancelPendingTransition();
        Assert.Equal(ElasticBreathState.Working, _engine.Snapshot.State);
    }
}
