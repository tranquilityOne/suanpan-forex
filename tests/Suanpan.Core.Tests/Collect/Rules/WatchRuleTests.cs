using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Reference.Enums;
using Xunit;

namespace Suanpan.Core.Tests.Collect.Rules;

/// <summary>盯盘的规则（采集设计 §3.5，BR-013 ~ BR-018、ERR-COL-005）</summary>
public class WatchRuleTests
{
    private static readonly DateTime ScheduledAt = new(2026, 11, 6, 13, 30, 0, DateTimeKind.Utc);

    /// <summary>BR-018：只盯精确到分钟的事件；中重要性只在配置打开时盯，低重要性不盯</summary>
    [Theory]
    [InlineData(Importance.High, false, TimePrecision.Minute, true)]
    [InlineData(Importance.Medium, false, TimePrecision.Minute, false)]
    [InlineData(Importance.Medium, true, TimePrecision.Minute, true)]
    [InlineData(Importance.Low, true, TimePrecision.Minute, false)]
    [InlineData(Importance.High, false, TimePrecision.DateOnly, false)]
    [InlineData(Importance.High, false, TimePrecision.ToBeDetermined, false)]
    public void IsEligible(Importance importance, bool includeMedium, TimePrecision precision, bool expected)
        => Assert.Equal(expected, WatchRule.IsEligible(importance, includeMedium, precision));

    /// <summary>BR-013：提前和窗口取各事件中最大的，没配的用默认值；BR-014：间隔取数据源的配置或默认值</summary>
    [Fact]
    public void Merge_TakesTheLongestLeadAndWindow()
    {
        (short?, short?)[] events = [(null, null), (120, null), (null, 30)];

        var timing = WatchRule.Merge(events, null, 10, RateLimitUnit.PerSecond, WatchDefaults.Default);

        Assert.Equal(new WatchTiming(TimeSpan.FromSeconds(120), TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(2)), timing);
    }

    /// <summary>BR-014：间隔不小于 1 秒，也不小于频率上限换算的最小间隔（FR-003 验收第 5 条）</summary>
    [Theory]
    [InlineData((short)5, 10, RateLimitUnit.PerSecond, 5)]       // 数据源配了 5 秒
    [InlineData(null, 1, RateLimitUnit.PerMinute, 60)]           // 每分钟 1 次：至少 60 秒
    [InlineData(null, 20, RateLimitUnit.PerMinute, 3)]           // 每分钟 20 次：至少 3 秒
    [InlineData((short)1, 1, RateLimitUnit.PerSecond, 1)]
    public void Merge_IntervalRespectsTheRateLimit(short? sourceInterval, int rateLimit, RateLimitUnit unit, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), WatchRule.Merge([(null, null)], sourceInterval, rateLimit, unit, WatchDefaults.Default).Interval);

    /// <summary>开始为 T − 提前；延迟判定为 T + 10 分钟（BR-017），窗口更短时为窗口结束</summary>
    [Theory]
    [InlineData(60, 15, "13:29:00", "13:40:00", "13:45:00")]
    [InlineData(60, 5, "13:29:00", "13:35:00", "13:35:00")]
    public void Window(int leadSeconds, int windowMinutes, string start, string delayAt, string end)
    {
        var window = WatchRule.Window(ScheduledAt, new WatchTiming(TimeSpan.FromSeconds(leadSeconds), TimeSpan.FromMinutes(windowMinutes), TimeSpan.FromSeconds(2)));

        Assert.Equal((At(start), At(delayAt), At(end)), (window.Start, window.DelayAt, window.End));
    }

    /// <summary>ERR-COL-005：429、403 加倍，最多 60 秒（原间隔更长时保持）；成功恢复；其他失败不变</summary>
    [Theory]
    [InlineData(2, 2, 429, 4)]
    [InlineData(32, 2, 403, 60)]
    [InlineData(60, 60, 429, 60)]
    [InlineData(90, 90, 429, 90)]
    [InlineData(16, 2, 200, 2)]
    [InlineData(16, 2, 503, 16)]
    [InlineData(16, 2, null, 16)]
    public void NextInterval(int currentSeconds, int normalSeconds, int? httpStatus, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds),
            WatchRule.NextInterval(TimeSpan.FromSeconds(currentSeconds), TimeSpan.FromSeconds(normalSeconds), httpStatus));

    /// <summary>拿到值：已公布、待复核，或公布值不为空（人工录入、补采）</summary>
    [Theory]
    [InlineData(CalendarEventStatus.Watching, false, false)]
    [InlineData(CalendarEventStatus.Delayed, false, false)]
    [InlineData(CalendarEventStatus.Watching, true, true)]
    [InlineData(CalendarEventStatus.Released, true, true)]
    [InlineData(CalendarEventStatus.PendingReview, false, true)]
    public void IsDone(CalendarEventStatus status, bool hasActual, bool expected)
        => Assert.Equal(expected, WatchRule.IsDone(new CalendarEvent { Status = status, Actual = hasActual ? 0.3m : null }));

    /// <summary>计划时长：配置超出范围用默认值；不短于最大提前秒数换算的分钟数加 1</summary>
    [Theory]
    [InlineData(5, 60, 5)]
    [InlineData(2, 60, 2)]
    [InlineData(2, 300, 6)]
    [InlineData(1, 60, WatchRule.DefaultPlanMinutes)]
    [InlineData(61, 60, WatchRule.DefaultPlanMinutes)]
    public void PlanMinutes(int configured, int maxLeadSeconds, int expected)
        => Assert.Equal(expected, WatchRule.PlanMinutes(configured, TimeSpan.FromSeconds(maxLeadSeconds)));

    /// <summary>BR-017：延迟后的前 24 小时每 5 分钟，之后每 60 分钟；距上一次运行满间隔才补采</summary>
    [Theory]
    [InlineData(23, 5)]
    [InlineData(24, 60)]
    public void BackfillInterval(int hoursSinceDelayed, int expectedMinutes)
        => Assert.Equal(TimeSpan.FromMinutes(expectedMinutes),
            WatchRule.BackfillInterval(ScheduledAt, ScheduledAt.AddHours(hoursSinceDelayed), BackfillSettings.Default));

    [Theory]
    [InlineData(null, true)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void NeedsBackfill(int? minutesSinceLastRun, bool expected)
    {
        var now = ScheduledAt.AddHours(1);
        DateTime? lastRun = minutesSinceLastRun is { } minutes ? now.AddMinutes(-minutes) : null;

        Assert.Equal(expected, WatchRule.NeedsBackfill(lastRun, TimeSpan.FromMinutes(5), now));
    }

    private static DateTime At(string time) => ScheduledAt.Date + TimeSpan.Parse(time, System.Globalization.CultureInfo.InvariantCulture);
}
