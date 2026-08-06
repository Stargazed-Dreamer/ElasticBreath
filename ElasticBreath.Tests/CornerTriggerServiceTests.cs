using System.Threading;
using System.Windows;
using ElasticBreath.App.Services;
using Xunit;
using Point = System.Windows.Point;

namespace ElasticBreath.Tests;

public class CornerTriggerServiceTests
{
    private static readonly Rect Bounds = new(0, 0, 1000, 1000);
    private static readonly Point Center = new(500, 500);

    [Fact]
    public void TryTrigger_NoTrigger_WhenCursorOutsideCorners()
    {
        var svc = new CornerTriggerService();
        var hover = TimeSpan.FromSeconds(1);
        Assert.False(svc.TryTrigger(Bounds, Center, hover));
    }

    [Theory]
    [InlineData(0, 0, "LT")]
    [InlineData(1000, 0, "RT")]
    [InlineData(0, 1000, "LB")]
    [InlineData(1000, 1000, "RB")]
    public void TryTrigger_TriggersAfterHoverDuration(double x, double y, string corner)
    {
        var svc = new CornerTriggerService();
        var cursor = new Point(x, y);
        var hover = TimeSpan.FromMilliseconds(80);

        // First call registers entry; returns false (waiting for hover).
        Assert.False(svc.TryTrigger(Bounds, cursor, hover));
        // The entered corner is exposed via hover progress.
        Assert.Equal(corner, svc.GetHoverProgress(hover).Corner);
        // Immediate second call: not enough elapsed time yet.
        Assert.False(svc.TryTrigger(Bounds, cursor, hover));
        // Wait past hover duration.
        Thread.Sleep(120);
        // Should trigger now.
        Assert.True(svc.TryTrigger(Bounds, cursor, hover));
    }

    [Fact]
    public void TryTrigger_DoesNotTrigger_BeforeHoverDurationCompletes()
    {
        var svc = new CornerTriggerService();
        var cursor = new Point(0, 0);
        var hover = TimeSpan.FromMilliseconds(150);

        Assert.False(svc.TryTrigger(Bounds, cursor, hover));
        // Without waiting long enough, still no trigger.
        Thread.Sleep(40);
        Assert.False(svc.TryTrigger(Bounds, cursor, hover));
    }

    [Fact]
    public void TryTrigger_MustLeaveCornerBeforeNextTrigger()
    {
        var svc = new CornerTriggerService();
        var cursor = new Point(0, 0);
        var hover = TimeSpan.FromMilliseconds(80);

        // First trigger sequence.
        Assert.False(svc.TryTrigger(Bounds, cursor, hover));
        Thread.Sleep(120);
        Assert.True(svc.TryTrigger(Bounds, cursor, hover));

        // Same corner immediately after trigger: must exit first → false.
        Assert.False(svc.TryTrigger(Bounds, cursor, hover));
        Thread.Sleep(120);
        // Still must exit; waiting alone does not re-arm.
        Assert.False(svc.TryTrigger(Bounds, cursor, hover));

        // Leave corner (cursor moves to center) → resets must-exit flag.
        Assert.False(svc.TryTrigger(Bounds, Center, hover));
        // Return to corner: new entry registered.
        Assert.False(svc.TryTrigger(Bounds, cursor, hover));
        Thread.Sleep(120);
        // Now triggers again.
        Assert.True(svc.TryTrigger(Bounds, cursor, hover));
    }

    [Fact]
    public void GetHoverProgress_ReturnsNull_WhenNotHovering()
    {
        var svc = new CornerTriggerService();
        var hover = TimeSpan.FromSeconds(1);
        var state = svc.GetHoverProgress(hover);
        Assert.Null(state.Corner);
        Assert.Equal(0, state.Progress);
    }

    [Fact]
    public void GetHoverProgress_ReturnsCornerAndGrows_AfterEntry()
    {
        var svc = new CornerTriggerService();
        var cursor = new Point(0, 0);
        var hover = TimeSpan.FromMilliseconds(150);

        svc.TryTrigger(Bounds, cursor, hover);
        Thread.Sleep(60);
        var state = svc.GetHoverProgress(hover);

        Assert.Equal("LT", state.Corner);
        Assert.InRange(state.Progress, 0.0, 1.0);
        Assert.True(state.Progress > 0, "progress should be growing after partial hover");
    }

    [Fact]
    public void GetHoverProgress_ReturnsNull_AfterTrigger()
    {
        var svc = new CornerTriggerService();
        var cursor = new Point(0, 0);
        var hover = TimeSpan.FromMilliseconds(80);

        svc.TryTrigger(Bounds, cursor, hover);
        Thread.Sleep(120);
        svc.TryTrigger(Bounds, cursor, hover); // triggers

        var state = svc.GetHoverProgress(hover);
        Assert.Null(state.Corner);
        Assert.Equal(0, state.Progress);
    }
}
