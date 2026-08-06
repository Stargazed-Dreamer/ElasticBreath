using ElasticBreath.App.Domain;
using ElasticBreath.App.Services;
using Xunit;

namespace ElasticBreath.Tests;

public class PostponeServiceTests
{
    private static ElasticBreathSettings NewSettings() => new ElasticBreathSettings().Sanitize();

    [Fact]
    public void CanPostpone_False_ForSafePressure()
    {
        var svc = new PostponeService(NewSettings());
        Assert.False(svc.CanPostpone(ElasticBreathState.Working, WorkingPressureLevel.Safe));
    }

    [Fact]
    public void CanPostpone_True_ForWarningPressure()
    {
        var now = DateTime.UtcNow;
        var svc = new PostponeService(NewSettings(), () => now);
        Assert.True(svc.CanPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning));
    }

    [Fact]
    public void CanPostpone_True_ForHardPressure()
    {
        var now = DateTime.UtcNow;
        var svc = new PostponeService(NewSettings(), () => now);
        Assert.True(svc.CanPostpone(ElasticBreathState.Working, WorkingPressureLevel.Hard));
    }

    [Fact]
    public void CanPostpone_False_WhenIdleEvenWithWarning()
    {
        var svc = new PostponeService(NewSettings());
        Assert.False(svc.CanPostpone(ElasticBreathState.Idle, WorkingPressureLevel.Warning));
    }

    [Fact]
    public void TryPostpone_Success_IncrementsUsedAndDecrementsRemaining()
    {
        var now = DateTime.UtcNow;
        var settings = NewSettings();
        var svc = new PostponeService(settings, () => now);

        Assert.Equal(0, svc.PostponesUsedToday);
        Assert.Equal(settings.DailyPostponeLimit, svc.PostponesRemainingToday);

        Assert.True(svc.TryPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning));
        Assert.Equal(1, svc.PostponesUsedToday);
        Assert.Equal(settings.DailyPostponeLimit - 1, svc.PostponesRemainingToday);
    }

    [Fact]
    public void TryPostpone_AfterLimitExhausted_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var settings = NewSettings();
        var svc = new PostponeService(settings, () => now);

        for (var i = 0; i < settings.DailyPostponeLimit; i++)
        {
            Assert.True(svc.TryPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning));
            now = now + settings.PostponeCooldown + TimeSpan.FromSeconds(1);
        }

        Assert.Equal(0, svc.PostponesRemainingToday);
        Assert.False(svc.CanPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning));
        Assert.False(svc.TryPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning));
    }

    [Fact]
    public void IsInCooldown_True_AfterPostpone()
    {
        var now = DateTime.UtcNow;
        var settings = NewSettings();
        var svc = new PostponeService(settings, () => now);

        Assert.False(svc.IsInCooldown());
        svc.TryPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning);
        Assert.True(svc.IsInCooldown());
    }

    [Fact]
    public void IsInCooldown_False_AfterCooldownElapses()
    {
        var now = DateTime.UtcNow;
        var settings = NewSettings();
        var svc = new PostponeService(settings, () => now);

        svc.TryPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning);
        Assert.True(svc.IsInCooldown());

        // Advance the virtual clock past the cooldown window.
        now = now + settings.PostponeCooldown + TimeSpan.FromSeconds(1);
        Assert.False(svc.IsInCooldown());
    }

    [Fact]
    public void CanPostpone_False_DuringCooldown()
    {
        var now = DateTime.UtcNow;
        var settings = NewSettings();
        var svc = new PostponeService(settings, () => now);

        svc.TryPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning);
        Assert.True(svc.IsInCooldown());
        Assert.False(svc.CanPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning));
    }

    [Fact]
    public void NotifyRestCompleted_ResetsQuota_WhenDurationAboveThreshold()
    {
        var now = DateTime.UtcNow;
        var settings = NewSettings();
        var svc = new PostponeService(settings, () => now);

        svc.TryPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning);
        Assert.Equal(1, svc.PostponesUsedToday);

        svc.NotifyRestCompleted(settings.MinEffectiveRestThreshold);
        Assert.Equal(0, svc.PostponesUsedToday);
    }

    [Fact]
    public void NotifyRestCompleted_DoesNotReset_WhenDurationBelowThreshold()
    {
        var now = DateTime.UtcNow;
        var settings = NewSettings();
        var svc = new PostponeService(settings, () => now);

        svc.TryPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning);
        Assert.Equal(1, svc.PostponesUsedToday);

        var shortRest = settings.MinEffectiveRestThreshold - TimeSpan.FromSeconds(1);
        svc.NotifyRestCompleted(shortRest);
        Assert.Equal(1, svc.PostponesUsedToday);
    }

    [Fact]
    public void ResetDailyIfNeeded_ResetsCount_WhenDateChanges()
    {
        var now = DateTime.UtcNow;
        var settings = NewSettings();
        var svc = new PostponeService(settings, () => now);

        svc.TryPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning);
        Assert.Equal(1, svc.PostponesUsedToday);

        // Advance to a different local day.
        now = now.AddDays(2);
        svc.ResetDailyIfNeeded(DateOnly.FromDateTime(now.ToLocalTime()));
        Assert.Equal(0, svc.PostponesUsedToday);
    }

    [Fact]
    public void BuildSnapshot_ReflectsCanPostponeAndRemaining()
    {
        var now = DateTime.UtcNow;
        var settings = NewSettings();
        var svc = new PostponeService(settings, () => now);

        var snap = svc.BuildSnapshot(ElasticBreathState.Working, WorkingPressureLevel.Warning);
        Assert.True(snap.CanPostpone);
        Assert.Equal(0, snap.PostponesUsedToday);
        Assert.Equal(settings.DailyPostponeLimit, snap.PostponesRemainingToday);
        Assert.Equal(settings.DailyPostponeLimit, snap.DailyLimit);
        Assert.Equal(TimeSpan.Zero, snap.CooldownRemaining);
    }

    [Fact]
    public void BuildSnapshot_ReflectsCooldownAfterPostpone()
    {
        var now = DateTime.UtcNow;
        var settings = NewSettings();
        var svc = new PostponeService(settings, () => now);

        svc.TryPostpone(ElasticBreathState.Working, WorkingPressureLevel.Warning);
        var snap = svc.BuildSnapshot(ElasticBreathState.Working, WorkingPressureLevel.Warning);

        Assert.False(snap.CanPostpone);
        Assert.Equal(1, snap.PostponesUsedToday);
        Assert.Equal(settings.DailyPostponeLimit - 1, snap.PostponesRemainingToday);
        Assert.True(snap.CooldownRemaining > TimeSpan.Zero);
        Assert.Equal(settings.PostponeCooldown, snap.CooldownRemaining);
    }
}
