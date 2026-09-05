using ElasticBreath.App.Services;
using ElasticBreath.Rendering;
using Xunit;

namespace ElasticBreath.Tests;

/// <summary>
/// SecondaryMonitorFlashService.ResolveVisibleState 的纯决策测试：
/// 副屏仅在与主屏一致的呼吸状态可见、功能开启、未被抑制且该屏正是鼠标所在屏时才显示。
/// </summary>
public class SecondaryMonitorFlashServiceTests
{
    [Fact]
    public void ShowsBreathing_OnCursorScreen_WhenEnabledAndVisible()
    {
        var resolved = SecondaryMonitorFlashService.ResolveVisibleState(
            enabled: true,
            suppressAll: false,
            state: EdgeOverlayState.Warning,
            isCursorScreen: true);

        Assert.Equal(EdgeOverlayState.Warning, resolved);
    }

    [Fact]
    public void Hidden_OnNonCursorSecondaryScreens()
    {
        var resolved = SecondaryMonitorFlashService.ResolveVisibleState(
            enabled: true,
            suppressAll: false,
            state: EdgeOverlayState.Warning,
            isCursorScreen: false);

        Assert.Equal(EdgeOverlayState.Hidden, resolved);
    }

    [Theory]
    [InlineData(false, false, true)]  // 功能关闭
    [InlineData(true, true, true)]    // 全屏抑制
    [InlineData(true, false, false)]  // 非鼠标所在屏
    public void Hidden_WhenDisabledSuppressedOrNotCursorScreen(bool enabled, bool suppressAll, bool isCursorScreen)
    {
        var resolved = SecondaryMonitorFlashService.ResolveVisibleState(
            enabled,
            suppressAll,
            EdgeOverlayState.RestBase,
            isCursorScreen);

        Assert.Equal(EdgeOverlayState.Hidden, resolved);
    }

    [Fact]
    public void Hidden_WhenOverlayStateHidden()
    {
        var resolved = SecondaryMonitorFlashService.ResolveVisibleState(
            enabled: true,
            suppressAll: false,
            state: EdgeOverlayState.Hidden,
            isCursorScreen: true);

        Assert.Equal(EdgeOverlayState.Hidden, resolved);
    }
}
