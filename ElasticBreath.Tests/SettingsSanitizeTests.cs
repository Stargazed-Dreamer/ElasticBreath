using ElasticBreath.App.Domain;
using Xunit;

namespace ElasticBreath.Tests;

public class SettingsSanitizeTests
{
    [Fact]
    public void Sanitize_DefaultValuesAreInRange()
    {
        var s = new ElasticBreathSettings().Sanitize();
        Assert.Equal(35 * 60, s.MinWorkSeconds);
        Assert.Equal(45 * 60, s.MaxWorkSeconds);
        Assert.Equal(5 * 60, s.DefaultRestSeconds);
        Assert.Equal(8 * 60, s.RestOvertimeSeconds);
        Assert.Equal(3 * 60, s.MinEffectiveRestSeconds);
        // AwayThreshold default (180) is below RestOvertime default (480), so Sanitize
        // clamps it up to RestOvertimeSeconds + 1 to satisfy the dependency rule.
        Assert.Equal(s.RestOvertimeSeconds + 1, s.AwayThresholdSeconds);
        Assert.Equal(1.5, s.CornerHoverSeconds);
        Assert.Equal("zh-CN", s.Language);
        Assert.Equal("auto", s.PreferredDisplay);
    }

    [Fact]
    public void Sanitize_ClampsMinWorkSecondsToLowerBound()
    {
        var s = new ElasticBreathSettings { MinWorkSeconds = -5 };
        s.Sanitize();
        Assert.Equal(ElasticBreathSettings.MinWorkSecondsMin, s.MinWorkSeconds);
    }

    [Fact]
    public void Sanitize_ClampsMinWorkSecondsToUpperBound()
    {
        var s = new ElasticBreathSettings { MinWorkSeconds = 999999 };
        s.Sanitize();
        Assert.Equal(ElasticBreathSettings.MinWorkSecondsMax, s.MinWorkSeconds);
    }

    [Fact]
    public void Sanitize_MaxWorkSecondsAtLeastMinWorkSeconds()
    {
        var s = new ElasticBreathSettings { MinWorkSeconds = 3000, MaxWorkSeconds = 100 };
        s.Sanitize();
        Assert.Equal(3000, s.MinWorkSeconds);
        Assert.Equal(3000, s.MaxWorkSeconds);
    }

    [Fact]
    public void Sanitize_RestOvertimeAtLeastDefaultRest()
    {
        var s = new ElasticBreathSettings { DefaultRestSeconds = 300, RestOvertimeSeconds = 100 };
        s.Sanitize();
        Assert.Equal(300, s.RestOvertimeSeconds);
    }

    [Fact]
    public void Sanitize_MinEffectiveRestAtMostRestOvertime()
    {
        var s = new ElasticBreathSettings { RestOvertimeSeconds = 480, MinEffectiveRestSeconds = 1000 };
        s.Sanitize();
        Assert.Equal(480, s.MinEffectiveRestSeconds);
    }

    [Fact]
    public void Sanitize_AwayThresholdGreaterThanRestOvertime()
    {
        var s = new ElasticBreathSettings { RestOvertimeSeconds = 480, AwayThresholdSeconds = 100 };
        s.Sanitize();
        Assert.True(s.AwayThresholdSeconds > s.RestOvertimeSeconds);
        Assert.Equal(s.RestOvertimeSeconds + 1, s.AwayThresholdSeconds);
    }

    [Fact]
    public void Sanitize_LanguageNull_DefaultsToZhCn()
    {
        var s = new ElasticBreathSettings { Language = null! };
        s.Sanitize();
        Assert.Equal("zh-CN", s.Language);
    }

    [Fact]
    public void Sanitize_LanguageWhitespace_DefaultsToZhCn()
    {
        var s = new ElasticBreathSettings { Language = "   " };
        s.Sanitize();
        Assert.Equal("zh-CN", s.Language);
    }

    [Fact]
    public void Sanitize_PreferredDisplayNull_DefaultsToAuto()
    {
        var s = new ElasticBreathSettings { PreferredDisplay = null! };
        s.Sanitize();
        Assert.Equal("auto", s.PreferredDisplay);
    }
}
