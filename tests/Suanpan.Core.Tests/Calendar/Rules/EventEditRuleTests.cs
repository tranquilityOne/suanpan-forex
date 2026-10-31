using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>人工操作的规则（日历设计 §3.7，BR-049 ~ BR-051）</summary>
public class EventEditRuleTests
{
    /// <summary>录入公布值：未公布、待复核的改为已公布，已公布的只改值；取消的不能改</summary>
    [Theory]
    [InlineData(CalendarEventStatus.Scheduled, CalendarEventStatus.Released)]
    [InlineData(CalendarEventStatus.Watching, CalendarEventStatus.Released)]
    [InlineData(CalendarEventStatus.Delayed, CalendarEventStatus.Released)]
    [InlineData(CalendarEventStatus.Unconfirmed, CalendarEventStatus.Released)]
    [InlineData(CalendarEventStatus.PendingReview, CalendarEventStatus.Released)]
    [InlineData(CalendarEventStatus.Released, CalendarEventStatus.Released)]
    [InlineData(CalendarEventStatus.Cancelled, null)]
    public void ActualTarget(CalendarEventStatus status, CalendarEventStatus? expected)
    {
        Assert.Equal(expected, EventEditRule.ActualTarget(status));
        Assert.True(expected is null || expected == status || CalendarEventStatusRule.CanTransition(status, expected.Value));
    }

    /// <summary>BR-051：驳回只适用于从未公布过的事件（公布值为空）；官方更正进入待复核的不能驳回</summary>
    [Theory]
    [InlineData(CalendarEventStatus.PendingReview, null, true)]
    [InlineData(CalendarEventStatus.PendingReview, "0.3", false)]
    [InlineData(CalendarEventStatus.Delayed, null, false)]
    public void CanReject(CalendarEventStatus status, string? actual, bool expected)
        => Assert.Equal(expected, EventEditRule.CanReject(Event(status, actual)));

    /// <summary>BR-052：已公布、官方更正进入待复核的算公布过；校验未通过进入待复核的没有公布过</summary>
    [Theory]
    [InlineData(CalendarEventStatus.Released, "0.3", true)]
    [InlineData(CalendarEventStatus.PendingReview, "0.3", true)]
    [InlineData(CalendarEventStatus.PendingReview, null, false)]
    [InlineData(CalendarEventStatus.Scheduled, null, false)]
    public void WasPublished(CalendarEventStatus status, string? actual, bool expected)
        => Assert.Equal(expected, EventEditRule.WasPublished(Event(status, actual)));

    private static CalendarEvent Event(CalendarEventStatus status, string? actual)
        => new() { Status = status, Actual = actual is null ? null : decimal.Parse(actual, System.Globalization.CultureInfo.InvariantCulture) };
}
