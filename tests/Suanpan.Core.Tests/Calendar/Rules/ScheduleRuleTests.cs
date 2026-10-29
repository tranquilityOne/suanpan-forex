using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Xunit;

using S = Suanpan.Core.Calendar.Enums.CalendarEventStatus;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>日程条目落到已有事件上时怎么处理（日历设计 §3.4，BR-039、BR-040，PRD §8.1.3）</summary>
public class ScheduleRuleTests
{
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

    private static CalendarEvent Event(S status, TimePrecision precision = TimePrecision.Minute) => new()
    {
        IndicatorCode = "US.NFP",
        ReferencePeriod = "2026-10",
        Status = status,
        ScheduledAt = Current,
        TimePrecision = precision,
    };
}
