using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Core.Collect.Rules;

/// <summary>
/// 盯盘（FR-003，采集设计 §3.5、§5.7，BR-013 ~ BR-018）：哪些事件要盯、一路盯盘的时间参数、退避、延迟后的补采间隔。
/// 默认值和范围是系统配置、数据源、指标映射共用的常量；配置值超出范围时用默认值
/// </summary>
public static class WatchRule
{
    /// <summary>提前秒数（BR-013）：默认 60，可按指标在"公布值"映射上配置</summary>
    public const int DefaultLeadSeconds = 60;
    public const int MinLeadSeconds = 10;
    public const int MaxLeadSeconds = 600;

    /// <summary>窗口：排期后多少分钟结束（BR-013）</summary>
    public const int DefaultWindowMinutes = 15;
    public const int MinWindowMinutes = 5;
    public const int MaxWindowMinutes = 120;

    /// <summary>请求间隔（BR-014）：默认 2 秒、最小 1 秒，可按数据源配置</summary>
    public const int DefaultIntervalSeconds = 2;
    public const int MinIntervalSeconds = 1;
    public const int MaxIntervalSeconds = 60;

    /// <summary>盯盘计划看未来多少分钟（PRD FR-003 主流程第 1 步）</summary>
    public const int DefaultPlanMinutes = 10;
    public const int MinPlanMinutes = 2;
    public const int MaxPlanMinutes = 60;

    /// <summary>延迟后的补采（BR-017）：先每 5 分钟，24 小时后每 60 分钟</summary>
    public const int DefaultBackfillMinutes = 5;
    public const int DefaultBackfillHours = 24;
    public const int DefaultBackfillSlowMinutes = 60;
    public const int MinBackfillMinutes = 1;
    public const int MaxBackfillMinutes = 1440;
    public const int MaxBackfillHours = 720;

    /// <summary>排期后多久仍无值算延迟（BR-017、BR-129 的口径，不做配置）</summary>
    public static readonly TimeSpan DelayAfter = TimeSpan.FromMinutes(10);

    /// <summary>被限流时间隔加倍的上限（ERR-COL-005）</summary>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 要盯的事件（BR-018）：重要性为高（includeMedium 时也包括中），时间精度为精确到分钟。状态由调用方按场景筛选
    /// （计划取待公布，服务重启后还取仍在窗口内的盯盘中）
    /// </summary>
    public static bool IsEligible(Importance importance, bool includeMedium, TimePrecision precision)
        => precision == TimePrecision.Minute && (importance == Importance.High || (includeMedium && importance == Importance.Medium));

    /// <summary>
    /// 一路盯盘的时间参数（BR-013、BR-014）：提前和窗口取各事件中最大的（没配的用默认值）；间隔取数据源的配置或默认值，
    /// 不小于 1 秒，也不小于频率上限换算的最小间隔（每秒 n 次为 1/n 秒，每分 n 次为 60/n 秒）
    /// </summary>
    public static WatchTiming Merge(
        IEnumerable<(short? LeadSeconds, short? WindowMinutes)> events, short? sourceIntervalSeconds, int rateLimit, RateLimitUnit rateLimitUnit,
        WatchDefaults defaults)
    {
        var items = events.ToList();
        var lead = items.Count == 0 ? defaults.LeadSeconds : items.Max(e => e.LeadSeconds ?? defaults.LeadSeconds);
        var window = items.Count == 0 ? defaults.WindowMinutes : items.Max(e => e.WindowMinutes ?? defaults.WindowMinutes);
        var interval = TimeSpan.FromSeconds(Math.Max(sourceIntervalSeconds ?? defaults.IntervalSeconds, MinIntervalSeconds));
        var byRateLimit = rateLimit > 0 ? DataSourceRule.RateLimitWindow(rateLimitUnit) / rateLimit : TimeSpan.Zero;
        return new WatchTiming(TimeSpan.FromSeconds(lead), TimeSpan.FromMinutes(window), interval > byRateLimit ? interval : byRateLimit);
    }

    /// <summary>开始（T − 提前）、延迟判定（T + 10 分钟，窗口更短时为窗口结束）、结束（T + 窗口）</summary>
    public static WatchWindow Window(DateTime scheduledAt, WatchTiming timing)
    {
        var end = scheduledAt + timing.Window;
        var delayAt = scheduledAt + DelayAfter;
        return new WatchWindow(scheduledAt - timing.Lead, delayAt < end ? delayAt : end, end);
    }

    /// <summary>
    /// 下一次请求前的间隔（ERR-COL-005）：429、403 时加倍，最多 60 秒（原间隔本来就更长时保持原间隔）；
    /// 成功（2xx）时恢复原间隔；其他失败（超时为空、5xx 等）不变
    /// </summary>
    public static TimeSpan NextInterval(TimeSpan current, TimeSpan normal, int? httpStatus)
    {
        if (httpStatus is 429 or 403)
        {
            var doubled = current * 2 < MaxBackoff ? current * 2 : MaxBackoff;
            return doubled > normal ? doubled : normal;
        }

        return httpStatus is >= 200 and < 300 ? normal : current;
    }

    /// <summary>拿到值：已公布、待复核，或公布值不为空（人工录入、补采也算），退出本路盯盘</summary>
    public static bool IsDone(CalendarEvent calendarEvent)
        => calendarEvent.Status is CalendarEventStatus.Released or CalendarEventStatus.PendingReview || calendarEvent.Actual is not null;

    /// <summary>
    /// 计划看未来多少分钟：取配置值（超出范围用默认值），不短于最大提前秒数换算的分钟数加 1，保证到开始时间前已经计划好
    /// </summary>
    public static int PlanMinutes(int configured, TimeSpan maxLead)
    {
        var minutes = configured is >= MinPlanMinutes and <= MaxPlanMinutes ? configured : DefaultPlanMinutes;
        return Math.Max(minutes, (int)Math.Ceiling(maxLead.TotalMinutes) + 1);
    }

    /// <summary>延迟后的补采间隔（BR-017）：延迟后的前若干小时为快间隔，之后为慢间隔</summary>
    public static TimeSpan BackfillInterval(DateTime delayedAt, DateTime now, BackfillSettings settings)
        => now - delayedAt < TimeSpan.FromHours(settings.Hours)
            ? TimeSpan.FromMinutes(settings.Minutes)
            : TimeSpan.FromMinutes(settings.SlowMinutes);

    /// <summary>距这个任务上一次运行已过了补采间隔（从没运行过也要补采）</summary>
    public static bool NeedsBackfill(DateTime? lastRunAt, TimeSpan interval, DateTime now) => lastRunAt is not { } last || now - last >= interval;

    /// <summary>配置值在范围内时用它，否则用默认值</summary>
    public static int OrDefault(int value, int min, int max, int defaultValue) => value >= min && value <= max ? value : defaultValue;
}

/// <summary>系统配置中的默认提前秒数、窗口分钟、请求间隔秒数</summary>
public sealed record WatchDefaults(int LeadSeconds, int WindowMinutes, int IntervalSeconds)
{
    public static readonly WatchDefaults Default = new(WatchRule.DefaultLeadSeconds, WatchRule.DefaultWindowMinutes, WatchRule.DefaultIntervalSeconds);
}

/// <summary>一路盯盘的提前、窗口和请求间隔</summary>
public sealed record WatchTiming(TimeSpan Lead, TimeSpan Window, TimeSpan Interval);

/// <summary>一路盯盘的开始、延迟判定、结束时间（UTC）</summary>
public sealed record WatchWindow(DateTime Start, DateTime DelayAt, DateTime End);

/// <summary>延迟后的补采：快间隔（分钟）、按快间隔补采多少小时、之后的慢间隔（分钟）</summary>
public sealed record BackfillSettings(int Minutes, int Hours, int SlowMinutes)
{
    public static readonly BackfillSettings Default = new(WatchRule.DefaultBackfillMinutes, WatchRule.DefaultBackfillHours, WatchRule.DefaultBackfillSlowMinutes);
}
