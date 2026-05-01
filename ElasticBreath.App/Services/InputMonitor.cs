using ElasticBreath.App.Interop;

namespace ElasticBreath.App.Services;

public readonly record struct InputSample(
    TimeSpan IdleDuration,
    bool HadActivity,
    double CursorMovePixels,
    TimeSpan DenseInputDuration,
    bool FilteredAsRemoteInput);

public sealed class InputMonitor
{
    private readonly RemoteInputFilterService _remoteInputFilterService;
    private uint _previousLastInputTick;
    private Win32Native.Point _previousCursor;
    private bool _hasPrevious;
    private DateTime _lastSampleUtc = DateTime.UtcNow;
    private TimeSpan _denseInputDuration = TimeSpan.Zero;

    /* 暴露当前密集输入持续时长，供 UI 显示探测进度 */
    public TimeSpan CurrentDenseInputDuration => _denseInputDuration;

    public InputMonitor(RemoteInputFilterService remoteInputFilterService)
    {
        _remoteInputFilterService = remoteInputFilterService;
    }

    public InputSample Sample(TimeSpan denseInputGap)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastSampleUtc;
        _lastSampleUtc = now;

        var idleDuration = Win32Native.GetIdleDuration();
        var lastInputTick = Win32Native.GetLastInputTick();
        _ = Win32Native.TryGetCursorPosition(out var cursor);
        var remoteForeground = _remoteInputFilterService.IsLikelyRemoteControlForeground();

        var hadActivity = false;
        var movePixels = 0d;
        if (_hasPrevious)
        {
            movePixels = Math.Sqrt(Math.Pow(cursor.X - _previousCursor.X, 2) + Math.Pow(cursor.Y - _previousCursor.Y, 2));
            var movedEnough = movePixels >= 5;
            hadActivity = movedEnough || lastInputTick != _previousLastInputTick;
        }

        if (remoteForeground)
        {
            hadActivity = false;
            _denseInputDuration = TimeSpan.Zero;
        }
        else if (idleDuration <= denseInputGap)
        {
            _denseInputDuration += elapsed;
        }
        else
        {
            _denseInputDuration = TimeSpan.Zero;
        }

        _previousCursor = cursor;
        _previousLastInputTick = lastInputTick;
        _hasPrevious = true;

        return new InputSample(idleDuration, hadActivity, movePixels, _denseInputDuration, remoteForeground);
    }
}
