using ElasticBreath.App.Interop;

namespace ElasticBreath.App.Services;

/// <summary>
/// 表示输入采样数据，包含空闲时间、活动状态等信息。
/// </summary>
public readonly record struct InputSample(
    TimeSpan IdleDuration,
    bool HadActivity,
    double CursorMovePixels,
    TimeSpan DenseInputDuration,
    bool FilteredAsRemoteInput);

/// <summary>
/// 输入监控器，负责定期采样用户输入状态。
/// </summary>
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

    /// <summary>
    /// 初始化输入监控器实例。
    /// </summary>
    /// <param name="remoteInputFilterService">远程输入过滤服务，用于检测远程控制前台。</param>
    public InputMonitor(RemoteInputFilterService remoteInputFilterService)
    {
        _remoteInputFilterService = remoteInputFilterService;
    }

    /// <summary>
    /// 采样当前输入状态，返回输入采样数据。
    /// </summary>
    /// <param name="denseInputGap">密集输入间隔阈值，用于判断是否重置密集输入持续时长。</param>
    /// <returns>包含空闲时间、活动状态等信息的输入采样数据。</returns>
    public InputSample Sample(TimeSpan denseInputGap)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastSampleUtc; // 计算自上次采样以来的时间间隔
        _lastSampleUtc = now;

        var idleDuration = Win32Native.GetIdleDuration(); // 获取系统空闲时长
        var lastInputTick = Win32Native.GetLastInputTick(); // 获取最后输入的时间戳
        _ = Win32Native.TryGetCursorPosition(out var cursor); // 尝试获取当前光标位置
        var remoteForeground = _remoteInputFilterService.IsLikelyRemoteControlForeground(); // 检测远程控制前台状态

        var hadActivity = false;
        var movePixels = 0d;
        /// <summary>
        /// 如果存在之前的采样数据，则计算光标移动距离并判断是否有活动。
        /// </summary>
        if (_hasPrevious) // 如果有之前的采样数据，则进行比较
        {
            // 计算光标移动的欧几里得距离（像素）
            movePixels = Math.Sqrt(Math.Pow(cursor.X - _previousCursor.X, 2) + Math.Pow(cursor.Y - _previousCursor.Y, 2));
            // 判断光标移动是否足够显著（至少5像素）
            var movedEnough = movePixels >= 5;
            // 如果光标移动足够或输入时间戳变化，则视为有活动
            hadActivity = movedEnough || lastInputTick != _previousLastInputTick;
        }

/// <summary>
/// 如果检测到远程控制前台，则忽略活动并重置密集输入
/// </summary>
        if (remoteForeground) // 如果检测到远程控制前台，则忽略活动并重置密集输入
        {
            hadActivity = false; // 重置活动标志为 false，表示无用户活动
            _denseInputDuration = TimeSpan.Zero; // 将密集输入持续时间重置为零
        }
        else if (idleDuration <= denseInputGap) // 如果空闲时间不超过密集输入间隔，则累计密集输入时长
        {
            _denseInputDuration += elapsed;
        }
        else // 否则重置密集输入持续时长
        {
            _denseInputDuration = TimeSpan.Zero;
        }

        // 更新状态变量，为下次采样做准备
        _previousCursor = cursor;
        _previousLastInputTick = lastInputTick;
        _hasPrevious = true;

        return new InputSample(idleDuration, hadActivity, movePixels, _denseInputDuration, remoteForeground);
    }
}
