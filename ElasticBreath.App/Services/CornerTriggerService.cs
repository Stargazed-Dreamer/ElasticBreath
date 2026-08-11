using System.Windows;

namespace ElasticBreath.App.Services;

/// <summary>
/// 角落悬停的当前进度快照，供左上角指示圆视觉使用。
/// </summary>
/// <param name="Corner">角落标识，仅左上角 "LT"；无悬停时为 null</param>
/// <param name="Progress">填充进度 0.0~1.0</param>
public readonly record struct CornerHoverState(string? Corner, double Progress);

/// <summary>
/// CornerTriggerService 类，处理左上角悬停触发逻辑：
/// 当光标在屏幕左上角悬停指定时间后触发工作/休息切换。
/// 仅左上角参与触发——右上角邻近窗口关闭按钮、右下角是"显示桌面"热区、
/// 左下角可能是开始菜单，只有左上角最安全。
/// </summary>
public sealed class CornerTriggerService
{
    // 角落触发区域的大小（像素）
    private const double CornerHitSize = 18;
    // 当前激活的角落标识（仅 "LT"）
    private string? _activeCorner;
    // 光标进入角落的UTC时间
    private DateTime _enteredUtc;
    // 标志位，确保必须先离开角落才能再次触发，防止连续触发
    private bool _mustExitCornerBeforeNextTrigger;

    /// <summary>
    /// 尝试触发角落事件，基于光标在左上角区域内的悬停时间。
    /// </summary>
    /// <param name="bounds">窗口或区域的边界。</param>
    /// <param name="cursor">当前光标位置。</param>
    /// <param name="hoverDuration">光标需要悬停的时间阈值。</param>
    /// <returns>如果成功触发则返回 true，否则返回 false。</returns>
    public bool TryTrigger(Rect bounds, System.Windows.Point cursor, TimeSpan hoverDuration)
    {
        // 检测光标是否在左上角区域，如果不在则重置状态并返回 false
        var corner = DetectCorner(bounds, cursor);
        if (corner is null)
        {
            _activeCorner = null;
            _mustExitCornerBeforeNextTrigger = false;
            return false;
        }

        // 如果上次触发后还未离开角落，则不允许再次触发
        if (_mustExitCornerBeforeNextTrigger)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        // 如果光标进入新的角落，记录进入时间并返回 false（等待悬停）
        if (_activeCorner != corner)
        {
            _activeCorner = corner;
            _enteredUtc = now;
            return false;
        }

        // 如果悬停时间不足指定阈值，则返回 false
        if (now - _enteredUtc < hoverDuration)
        {
            return false;
        }

        // 悬停时间足够，触发事件：更新状态并标记需要退出才能再次触发
        _activeCorner = corner;
        _mustExitCornerBeforeNextTrigger = true;
        return true;
    }

    /// <summary>
    /// 返回当前角落悬停的填充进度，供左上角指示圆视觉绘制。
    /// 触发后（必须离开角落才能再次触发）保持进度 1.0，使圆保持绿色直到鼠标移开。
    /// </summary>
    /// <param name="hoverDuration">光标需要悬停的时间阈值</param>
    public CornerHoverState GetHoverProgress(TimeSpan hoverDuration)
    {
        if (_activeCorner is null)
        {
            return new CornerHoverState(null, 0);
        }

        // 触发后仍需显示"已切换"的满格状态，直到鼠标离开角落
        if (_mustExitCornerBeforeNextTrigger)
        {
            return new CornerHoverState(_activeCorner, 1.0);
        }

        var elapsed = DateTime.UtcNow - _enteredUtc;
        var progress = hoverDuration > TimeSpan.Zero
            ? Math.Clamp(elapsed.TotalSeconds / hoverDuration.TotalSeconds, 0.0, 1.0)
            : 0.0;
        return new CornerHoverState(_activeCorner, progress);
    }

    /// <summary>
    /// 检测光标是否在屏幕左上角区域。
    /// 仅左上角参与触发，其他三个角落一律不响应。
    /// </summary>
    /// <param name="bounds">窗口或区域的边界。</param>
    /// <param name="cursor">当前光标位置。</param>
    /// <returns>光标在左上角时返回 "LT"，否则返回 null。</returns>
    private static string? DetectCorner(Rect bounds, System.Windows.Point cursor)
    {
        var nearLeft = cursor.X <= bounds.Left + CornerHitSize;
        var nearTop = cursor.Y <= bounds.Top + CornerHitSize;
        return nearLeft && nearTop ? "LT" : null;
    }
}
