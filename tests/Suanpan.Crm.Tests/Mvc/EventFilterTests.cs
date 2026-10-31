using Suanpan.Crm.Models.Calendar;
using Xunit;

namespace Suanpan.Crm.Tests.Mvc;

/// <summary>事件列表默认本周（BR-048，日历设计 §12 第 17 项）：一周从周一开始；选了日期就不替换</summary>
public class EventFilterTests
{
    [Theory]
    [InlineData("2026-11-02", "2026-11-02")]   // 周一
    [InlineData("2026-11-08", "2026-11-02")]   // 周日
    [InlineData("2026-11-04", "2026-11-02")]
    public void WeekOf_StartsOnMonday(string day, string monday)
        => Assert.Equal((DateOnly.Parse(monday), DateOnly.Parse(monday).AddDays(6)), EventFilter.WeekOf(DateOnly.Parse(day)));

    [Fact]
    public void DefaultToWeekOf_KeepsChosenDates()
    {
        var chosen = new EventFilter { To = new DateOnly(2026, 12, 31) };

        chosen.DefaultToWeekOf(new DateOnly(2026, 11, 4));

        Assert.Equal(((DateOnly?)null, (DateOnly?)new DateOnly(2026, 12, 31)), (chosen.From, chosen.To));
    }
}
