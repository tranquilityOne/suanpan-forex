using System.Globalization;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Xunit;

using S = Suanpan.Core.Calendar.Enums.CalendarEventStatus;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>公布值的取舍（日历设计 §3.5、§5.2 ~ §5.4，BR-043 ~ BR-046、BR-157）</summary>
public class ReleaseValueRuleTests
{
    /// <summary>BR-043：按指标小数位四舍五入，.5 远离零（正负数都是），不是银行家舍入</summary>
    [Theory]
    [InlineData("0.25", (short)1, "0.3")]
    [InlineData("-0.25", (short)1, "-0.3")]
    [InlineData("0.35", (short)1, "0.4")]
    [InlineData("29.5", (short)0, "30")]
    [InlineData("0.2834", (short)1, "0.3")]
    public void Round_HalfAwayFromZero(string value, short decimals, string expected)
        => Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), ReleaseValueRule.Round(decimal.Parse(value, CultureInfo.InvariantCulture), decimals));

    /// <summary>最新一期的处理（§5.2 的表）</summary>
    [Theory]
    [InlineData(S.Scheduled, null, LatestAction.Release)]
    [InlineData(S.Watching, null, LatestAction.Release)]
    [InlineData(S.Delayed, null, LatestAction.Release)]
    [InlineData(S.Unconfirmed, null, LatestAction.Release)]
    [InlineData(S.Released, "254", LatestAction.ValueOnly)]
    [InlineData(S.Released, "250", LatestAction.Correction)]
    [InlineData(S.PendingReview, "250", LatestAction.ValueOnly)]
    [InlineData(S.Cancelled, null, LatestAction.ValueOnly)]
    public void Plan_DecidesTheLatestPeriodByStatus(S status, string? actual, LatestAction expected)
    {
        var latest = Event("2026-09", status, actual is null ? null : decimal.Parse(actual, CultureInfo.InvariantCulture));

        var plan = ReleaseValueRule.Plan([Point("2026-09", 254)], Events(latest), hasReleasedEvent: true);

        Assert.Equal(expected, plan.Action);
    }

    /// <summary>BR-157：最新一期没有事件时，有公布过的事件就补建，刚开始采集则只填下一期的前值、不改前值</summary>
    [Theory]
    [InlineData(true, LatestAction.Supplement)]
    [InlineData(false, LatestAction.InitialPrevious)]
    public void Plan_WithoutEventDependsOnWhetherAnythingWasReleased(bool hasReleasedEvent, LatestAction expected)
    {
        var plan = ReleaseValueRule.Plan([Point("2026-08", 22), Point("2026-09", 254)], Events(), hasReleasedEvent);

        Assert.Equal(expected, plan.Action);
        Assert.Equal(expected == LatestAction.Supplement, plan.Previous is not null);
    }

    /// <summary>公布值被人工锁定：只记数值，值不同时写锁定冲突（BR-062）</summary>
    [Theory]
    [InlineData(254, false)]
    [InlineData(260, true)]
    public void Plan_LockedActualIsNotOverwritten(int collected, bool conflict)
    {
        var latest = Event("2026-09", S.Released, 254);
        EventLockRule.Lock(latest.LockedFields, EventField.Actual, "editor", default, "人工修正");

        var plan = ReleaseValueRule.Plan([Point("2026-09", collected)], Events(latest), hasReleasedEvent: true);

        Assert.Equal((LatestAction.ValueOnly, conflict), (plan.Action, plan.ActualLockConflict));
    }

    /// <summary>
    /// BR-044：上一期有公布过的事件时，前值取它的公布值，数据中的值不同就是前值修正；
    /// 上一期和更早各期的值变了，更新各自的修正后数值（与修正后数值相同的不再更新）
    /// </summary>
    [Fact]
    public void Plan_PreviousAndRevisionsFollowTheData()
    {
        var july = Event("2026-07", S.Released, 50);
        july.RevisedValue = 72;
        var august = Event("2026-08", S.Released, 22);
        var june = Event("2026-06", S.Released, 13);
        var september = Event("2026-09", S.Scheduled, null);

        var plan = ReleaseValueRule.Plan(
            [Point("2026-06", 13), Point("2026-07", 72), Point("2026-08", -4), Point("2026-09", 254)],
            Events(june, july, august, september),
            hasReleasedEvent: true);

        Assert.Equal(new PreviousValues(22, null, -4), plan.Previous);
        Assert.Equal([("2026-08", -4m)], plan.Revisions.Select(r => (r.Event.ReferencePeriod, r.RevisedValue)));
    }

    /// <summary>BR-044 首期前值：上一期没有事件（或没有公布值）时，前值取数据中上一期的值，前值修正为空</summary>
    [Fact]
    public void Plan_FirstPeriodTakesPreviousFromTheData()
    {
        var september = Event("2026-09", S.Scheduled, null);

        var plan = ReleaseValueRule.Plan([Point("2026-08", 22), Point("2026-09", 254)], Events(september), hasReleasedEvent: false);

        Assert.Equal(LatestAction.Release, plan.Action);
        Assert.Equal(new PreviousValues(22, null, null), plan.Previous);
        Assert.Empty(plan.Revisions);
    }

    /// <summary>数据中只有一期时不改前值；参考期顺序不限，按参考期找最新一期</summary>
    [Fact]
    public void Plan_UsesTheLatestPeriodRegardlessOfOrder()
    {
        var plan = ReleaseValueRule.Plan([Point("2026-09", 254), Point("2026-08", 22)], Events(), hasReleasedEvent: false);
        var single = ReleaseValueRule.Plan([Point("2026-09", 254)], Events(Event("2026-09", S.Scheduled, null)), hasReleasedEvent: true);

        Assert.Equal("2026-09", plan.Latest.Period);
        Assert.Null(single.Previous);
    }

    private static ReleasePoint Point(string period, decimal value) => new(period, value, null);

    private static CalendarEvent Event(string period, S status, decimal? actual) => new()
    {
        IndicatorCode = "US.NFP",
        ReferencePeriod = period,
        Status = status,
        Actual = actual,
    };

    private static Dictionary<string, CalendarEvent> Events(params CalendarEvent[] events) => events.ToDictionary(e => e.ReferencePeriod);
}
