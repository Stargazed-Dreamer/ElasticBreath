using System.Diagnostics;
using System.IO;

namespace ElasticBreath.App.UI;

/// <summary>
/// 临时渲染探针：统计各渲染入口的触发频率与耗时，
/// 用于定位“GPU 始终 10%”的占用源。定位完成后应整文件删除。
/// 不订阅 CompositionTarget.Rendering（会强制 WPF 持续渲染导致卡顿）。
/// 输出：每 10 秒追加一行到 Path.GetTempPath()/ElasticBreath/probe.log。
/// </summary>
internal static class RenderProbe
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "ElasticBreath", "probe.log");
    private static readonly double TickToMs = 1000.0 / Stopwatch.Frequency;
    private static readonly long DumpIntervalTicks = Stopwatch.Frequency * 10; // 10 秒

    private static long _snapshotRenders;
    private static long _snapshotTicks;
    private static long _overlayFrames;
    private static long _overlaySkipped;
    private static long _overlayRenderTicks;
    private static long _lastDumpTicks = Stopwatch.GetTimestamp();

    /// <summary>MainWindow.RenderSnapshot 入口调用</summary>
    public static void OnSnapshotRender(long elapsedStopwatchTicks)
    {
        Interlocked.Increment(ref _snapshotRenders);
        Interlocked.Add(ref _snapshotTicks, elapsedStopwatchTicks);
        MaybeDump();
    }

    /// <summary>EdgeOverlayWindow.RenderFrame 入口调用</summary>
    public static void OnOverlayFrame(bool skipped, long elapsedStopwatchTicks)
    {
        Interlocked.Increment(ref _overlayFrames);
        if (skipped) Interlocked.Increment(ref _overlaySkipped);
        Interlocked.Add(ref _overlayRenderTicks, elapsedStopwatchTicks);
        MaybeDump();
    }

    private static void MaybeDump()
    {
        var now = Stopwatch.GetTimestamp();
        if (now - _lastDumpTicks < DumpIntervalTicks)
            return;

        var snaps = Interlocked.Exchange(ref _snapshotRenders, 0);
        var snapTicks = Interlocked.Exchange(ref _snapshotTicks, 0);
        var ovlFrames = Interlocked.Exchange(ref _overlayFrames, 0);
        var ovlSkipped = Interlocked.Exchange(ref _overlaySkipped, 0);
        var ovlTicks = Interlocked.Exchange(ref _overlayRenderTicks, 0);
        _lastDumpTicks = now;

        var line = $"{DateTime.Now:HH:mm:ss} | " +
                   $"snapshot={snaps}x/{snapTicks * TickToMs:F1}ms | " +
                   $"overlay={ovlFrames}x(skip={ovlSkipped})/{ovlTicks * TickToMs:F1}ms" +
                   Environment.NewLine;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, line);
        }
        catch
        {
            // 探针日志写入失败不影响主程序
        }
    }
}
