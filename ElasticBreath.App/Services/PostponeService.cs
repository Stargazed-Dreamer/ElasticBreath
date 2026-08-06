using ElasticBreath.App.Domain;

namespace ElasticBreath.App.Services;

/// <summary>
/// PostponeService 类，负责推迟休息功能的状态跟踪与约束执行。
/// 实现 design §8.2：在预警/硬性区推迟后进入冷却（此期间不再因空闲触发提醒），
/// 每日推迟上限，以及"完整休息"（休息时长 ≥ 最短有效休息时长）后重置每日配额。
/// 运行时状态仅保存在内存中，重启后配额恢复为满额。
/// </summary>
public sealed class PostponeService
{
    private readonly ElasticBreathSettings _settings;
    private readonly Func<DateTime> _utcNowProvider;

    /* 最近一次推迟的 UTC 时间；DateTime.MinValue 表示尚未推迟过 */
    private DateTime _lastPostponeUtc = DateTime.MinValue;
    /* 今日已使用推迟次数 */
    private int _postponesUsedToday;
    /* 最近一次推迟所在的本地日期，用于跨日重置配额 */
    private DateOnly _quotaDate = DateOnly.MinValue;

    /// <summary>
    /// 初始化 PostponeService 实例。
    /// </summary>
    /// <param name="settings">弹性呼吸设置（读取冷却时长与每日上限）</param>
    /// <param name="utcNowProvider">UTC 时间提供器，便于单元测试注入虚拟时钟；默认为 DateTime.UtcNow</param>
    public PostponeService(ElasticBreathSettings settings, Func<DateTime>? utcNowProvider = null)
    {
        _settings = settings;
        _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
    }

    /// <summary>今日已使用推迟次数</summary>
    public int PostponesUsedToday => _postponesUsedToday;

    /// <summary>今日剩余推迟次数（不小于 0）</summary>
    public int PostponesRemainingToday
        => Math.Max(0, _settings.DailyPostponeLimit - _postponesUsedToday);

    /// <summary>
    /// 跨日重置检查：若本地日期已变更，将今日已用次数清零。
    /// 由引擎在每秒 Tick 中调用。
    /// </summary>
    /// <param name="today">当前本地日期</param>
    public void ResetDailyIfNeeded(DateOnly today)
    {
        if (_quotaDate != DateOnly.MinValue && _quotaDate != today)
        {
            _postponesUsedToday = 0;
        }
    }

    /// <summary>
    /// 判断当前是否处于推迟冷却期内。
    /// 冷却期内，引擎不再因空闲自动触发"工作→休息"切换。
    /// </summary>
    public bool IsInCooldown()
    {
        if (_lastPostponeUtc == DateTime.MinValue)
        {
            return false;
        }
        return _utcNowProvider() - _lastPostponeUtc < _settings.PostponeCooldown;
    }

    /// <summary>
    /// 计算冷却剩余时间。未在冷却中或从未推迟过则返回 Zero。
    /// </summary>
    public TimeSpan CooldownRemaining()
    {
        if (_lastPostponeUtc == DateTime.MinValue)
        {
            return TimeSpan.Zero;
        }
        var elapsed = _utcNowProvider() - _lastPostponeUtc;
        var remaining = _settings.PostponeCooldown - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// 判断当前是否允许推迟。
    /// 条件：处于工作状态、压力非安全区（Warning/Hard）、不在冷却期内、今日配额未用完。
    /// </summary>
    /// <param name="state">当前引擎状态</param>
    /// <param name="pressure">当前工作压力等级</param>
    public bool CanPostpone(ElasticBreathState state, WorkingPressureLevel pressure)
    {
        if (state != ElasticBreathState.Working || pressure == WorkingPressureLevel.Safe)
        {
            return false;
        }
        EnsureDailyReset();
        if (_postponesUsedToday >= _settings.DailyPostponeLimit)
        {
            return false;
        }
        return !IsInCooldown();
    }

    /// <summary>
    /// 尝试执行一次推迟。成功时累加今日次数、记录推迟时间。
    /// 调用前应先用 <see cref="CanPostpone"/> 判定，或直接调用本方法并检查返回值。
    /// </summary>
    /// <param name="state">当前引擎状态</param>
    /// <param name="pressure">当前工作压力等级</param>
    /// <returns>成功推迟返回 true；否则返回 false</returns>
    public bool TryPostpone(ElasticBreathState state, WorkingPressureLevel pressure)
    {
        if (!CanPostpone(state, pressure))
        {
            return false;
        }
        _postponesUsedToday++;
        _lastPostponeUtc = _utcNowProvider();
        _quotaDate = DateOnly.FromDateTime(_utcNowProvider().ToLocalTime());
        return true;
    }

    /// <summary>
    /// 通知一次休息已结束。若该次休息为"完整休息"
    /// （休息时长 ≥ 最短有效休息时长），则重置今日推迟配额。
    /// 由引擎在 Resting→Working/Idle 切换时调用。
    /// </summary>
    /// <param name="restDuration">本次休息已用时</param>
    public void NotifyRestCompleted(TimeSpan restDuration)
    {
        if (restDuration >= _settings.MinEffectiveRestThreshold)
        {
            _postponesUsedToday = 0;
        }
    }

    /// <summary>构建供 UI 消费的推迟状态快照</summary>
    public PostponeSnapshot BuildSnapshot(ElasticBreathState state, WorkingPressureLevel pressure)
    {
        EnsureDailyReset();
        return new PostponeSnapshot(
            CanPostpone(state, pressure),
            PostponesRemainingToday,
            _postponesUsedToday,
            _settings.DailyPostponeLimit,
            IsInCooldown() ? CooldownRemaining() : TimeSpan.Zero);
    }

    /// <summary>跨日重置（基于当前 UTC 时间对应的本地日期）</summary>
    private void EnsureDailyReset()
    {
        ResetDailyIfNeeded(DateOnly.FromDateTime(_utcNowProvider().ToLocalTime()));
    }
}
