using System.Windows;

namespace ElasticBreath.App.Services;

public sealed class CornerTriggerService
{
    private const double CornerHitSize = 18;
    private string? _activeCorner;
    private DateTime _enteredUtc;
    private DateTime _cooldownUntilUtc = DateTime.MinValue;

    public bool TryTrigger(Rect bounds, System.Windows.Point cursor, TimeSpan hoverDuration)
    {
        var now = DateTime.UtcNow;
        if (now < _cooldownUntilUtc)
        {
            return false;
        }

        var corner = DetectCorner(bounds, cursor);
        if (corner is null)
        {
            _activeCorner = null;
            return false;
        }

        if (_activeCorner != corner)
        {
            _activeCorner = corner;
            _enteredUtc = now;
            return false;
        }

        if (now - _enteredUtc < hoverDuration)
        {
            return false;
        }

        _activeCorner = null;
        _cooldownUntilUtc = now + TimeSpan.FromSeconds(1.2);
        return true;
    }

    private static string? DetectCorner(Rect bounds, System.Windows.Point cursor)
    {
        var left = bounds.Left;
        var top = bounds.Top;
        var right = bounds.Right;
        var bottom = bounds.Bottom;

        var nearLeft = cursor.X <= left + CornerHitSize;
        var nearRight = cursor.X >= right - CornerHitSize;
        var nearTop = cursor.Y <= top + CornerHitSize;
        var nearBottom = cursor.Y >= bottom - CornerHitSize;

        if (nearLeft && nearTop) return "LT";
        if (nearRight && nearTop) return "RT";
        if (nearLeft && nearBottom) return "LB";
        if (nearRight && nearBottom) return "RB";
        return null;
    }
}
