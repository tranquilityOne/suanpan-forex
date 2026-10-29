using Suanpan.Core.Calendar.Rules;
using Xunit;

using S = Suanpan.Core.Calendar.Enums.CalendarEventStatus;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>日历事件的状态流转（PRD §8.1.3，含 v1.0.0.22、v1.0.0.24 补的几条），日历设计 §3.2</summary>
public class CalendarEventStatusRuleTests
{
    /// <summary>状态机中的每一条边</summary>
    public static TheoryData<S, S> AllowedTransitions => new()
    {
        { S.Scheduled, S.Watching },
        { S.Scheduled, S.Released },
        { S.Scheduled, S.PendingReview },
        { S.Scheduled, S.Unconfirmed },
        { S.Scheduled, S.Cancelled },
        { S.Unconfirmed, S.Scheduled },
        { S.Unconfirmed, S.Released },
        { S.Unconfirmed, S.PendingReview },
        { S.Unconfirmed, S.Cancelled },
        { S.Watching, S.Released },
        { S.Watching, S.PendingReview },
        { S.Watching, S.Delayed },
        { S.Delayed, S.Scheduled },
        { S.Delayed, S.Released },
        { S.Delayed, S.PendingReview },
        { S.PendingReview, S.Released },
        { S.PendingReview, S.Delayed },
        { S.Released, S.PendingReview },
    };

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void CanTransition_FollowsTheStateMachine(S from, S to)
        => Assert.True(CalendarEventStatusRule.CanTransition(from, to));

    /// <summary>不在图中的流转都不允许：边的总数与上表一致，取消是终态</summary>
    [Fact]
    public void CanTransition_RejectsEverythingElse()
    {
        var all = Enum.GetValues<S>();
        var allowed = all.SelectMany(from => all.Where(to => CalendarEventStatusRule.CanTransition(from, to)).Select(to => (from, to))).ToList();

        Assert.Equal(AllowedTransitions.Count(), allowed.Count);
        Assert.Empty(CalendarEventStatusRule.NextStatuses(S.Cancelled));
        Assert.False(CalendarEventStatusRule.CanTransition(S.Released, S.Scheduled));
    }

    [Theory]
    [InlineData(S.Scheduled, true)]
    [InlineData(S.Watching, true)]
    [InlineData(S.Delayed, true)]
    [InlineData(S.Unconfirmed, true)]
    [InlineData(S.Released, false)]
    [InlineData(S.PendingReview, false)]
    [InlineData(S.Cancelled, false)]
    public void IsUnreleased_CoversStatusesBeforeRelease(S status, bool expected)
        => Assert.Equal(expected, CalendarEventStatusRule.IsUnreleased(status));
}
