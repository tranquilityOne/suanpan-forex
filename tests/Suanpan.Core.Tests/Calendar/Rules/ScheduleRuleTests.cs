using System.Globalization;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Xunit;

using S = Suanpan.Core.Calendar.Enums.CalendarEventStatus;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>日程条目落到已有事件上时怎么处理（日历设计 §3.4，BR-039、BR-040，PRD §8.1.3）</summary>
public class ScheduleRuleTests
{
    private const string NewYork = "America/New_York";

    private static readonly DateTime Current = new(2026, 11, 6, 13, 30, 0, DateTimeKind.Utc);

    private static readonly DateTime Moved = new(2026, 11, 20, 13, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(S.Scheduled, false, false, null)]
    [InlineData(S.Scheduled, true, true, null)]
    [InlineData(S.Watching, true, true, null)]
    [InlineData(S.Delayed, true, true, S.Scheduled)]
    [InlineData(S.Unconfirmed, false, false, S.Scheduled)]
    [InlineData(S.Unconfirmed, true, true, S.Scheduled)]
    public void Decide_ReschedulesAndRestoresUnreleasedEvents(S status, bool moved, bool reschedule, S? newStatus)
    {
        var decision = ScheduleRule.Decide(Event(status), moved ? Moved : Current, TimePrecision.Minute);

        Assert.Equal(new ScheduleDecision(false, reschedule, newStatus), decision);
    }

    /// <summary>已公布、待复核、取消的事件已经结束，排期不再随日程改</summary>
    [Theory]
    [InlineData(S.Released)]
    [InlineData(S.PendingReview)]
    [InlineData(S.Cancelled)]
    public void Decide_IgnoresFinishedEvents(S status)
        => Assert.Same(ScheduleDecision.Ignore, ScheduleRule.Decide(Event(status), Moved, TimePrecision.Minute));

    /// <summary>时间相同但精度变了（仅日期 → 精确到分钟）也算改期</summary>
    [Fact]
    public void Decide_PrecisionChangeIsReschedule()
    {
        var decision = ScheduleRule.Decide(Event(S.Scheduled, TimePrecision.DateOnly), Current, TimePrecision.Minute);

        Assert.True(decision.Reschedule);
    }

    /// <summary>状态被人工锁定时只改排期，不改状态（BR-062）</summary>
    [Fact]
    public void Decide_LockedStatusIsNotChanged()
    {
        var current = Event(S.Delayed);
        EventLockRule.Lock(current.LockedFields, EventField.Status, "editor", Current, "人工确认延迟");

        var decision = ScheduleRule.Decide(current, Moved, TimePrecision.Minute);

        Assert.Equal(new ScheduleDecision(false, true, null), decision);
    }

    [Theory]
    [InlineData(TimePrecision.Minute, true, true, true)]
    [InlineData(TimePrecision.Minute, true, false, false)]
    [InlineData(TimePrecision.DateOnly, true, false, true)]
    [InlineData(TimePrecision.DateOnly, false, false, false)]
    [InlineData(TimePrecision.ToBeDetermined, false, false, true)]
    public void ValidateEntry_RequiresWhatThePrecisionNeeds(TimePrecision precision, bool hasDate, bool hasTime, bool valid)
    {
        var error = ScheduleRule.ValidateEntry(hasDate ? new DateOnly(2026, 11, 6) : null, hasTime ? new TimeOnly(8, 30) : null, precision);

        Assert.Equal(valid, error is null);
    }

    /// <summary>精确到分钟的按当地时间换算；仅日期取当地 00:00；待定且没有日期时都为空（BR-037、BR-038）</summary>
    [Fact]
    public void Resolve_ConvertsByPrecision()
    {
        var minute = ScheduleRule.Resolve(new DateOnly(2026, 11, 6), new TimeOnly(8, 30), TimePrecision.Minute, NewYork);
        var dateOnly = ScheduleRule.Resolve(new DateOnly(2026, 11, 6), new TimeOnly(8, 30), TimePrecision.DateOnly, NewYork);
        var undetermined = ScheduleRule.Resolve(null, null, TimePrecision.ToBeDetermined, NewYork);

        Assert.Equal(new ResolvedSchedule(new DateTime(2026, 11, 6, 8, 30, 0), Current, LocalTimeResolution.Exact), minute);
        Assert.Equal(new ResolvedSchedule(new DateTime(2026, 11, 6, 0, 0, 0), new DateTime(2026, 11, 6, 5, 0, 0, DateTimeKind.Utc), LocalTimeResolution.Exact), dateOnly);
        Assert.Equal(new ResolvedSchedule(null, null, LocalTimeResolution.Exact), undetermined);
    }

    /// <summary>
    /// 只处理未来的条目（日历设计 §4.2）：现在是纽约 11 月 6 日 08:00（UTC 13:00）。精确到分钟的按 UTC 判断；
    /// 仅日期的按纽约的日期判断，当天算未来；待定且没有日期的算未来
    /// </summary>
    [Theory]
    [InlineData("2026-11-06 08:30", TimePrecision.Minute, true)]
    [InlineData("2026-11-06 07:30", TimePrecision.Minute, false)]
    [InlineData("2026-11-06", TimePrecision.DateOnly, true)]
    [InlineData("2026-11-05", TimePrecision.DateOnly, false)]
    [InlineData(null, TimePrecision.ToBeDetermined, true)]
    public void IsFuture_ComparesByPrecision(string? local, TimePrecision precision, bool expected)
    {
        var now = new DateTime(2026, 11, 6, 13, 0, 0, DateTimeKind.Utc);
        var value = local is null ? (DateTime?)null : DateTime.Parse(local, CultureInfo.InvariantCulture);
        var schedule = ScheduleRule.Resolve(
            value is { } v ? DateOnly.FromDateTime(v) : null, value is { } t ? TimeOnly.FromDateTime(t) : null, precision, NewYork);

        Assert.Equal(expected, ScheduleRule.IsFuture(schedule, precision, NewYork, now));
    }

    private static CalendarEvent Event(S status, TimePrecision precision = TimePrecision.Minute) => new()
    {
        IndicatorCode = "US.NFP",
        ReferencePeriod = "2026-10",
        Status = status,
        ScheduledAt = Current,
        TimePrecision = precision,
    };
}
