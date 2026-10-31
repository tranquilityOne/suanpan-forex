using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>预期值的"公布后"判断（BR-057，日历设计 §12 第 18 项）</summary>
public class ForecastRuleTests
{
    /// <summary>已公布、待复核、有公布值的算公布后，编辑不能再录入；取消的不能录入，管理员也不能更正</summary>
    [Theory]
    [InlineData(CalendarEventStatus.Scheduled, false, true, true)]
    [InlineData(CalendarEventStatus.Delayed, false, true, true)]
    [InlineData(CalendarEventStatus.Delayed, true, false, true)]       // 有公布值即算公布后，不论状态
    [InlineData(CalendarEventStatus.Released, true, false, true)]
    [InlineData(CalendarEventStatus.PendingReview, false, false, true)]
    [InlineData(CalendarEventStatus.Cancelled, false, false, false)]
    public void EnterAndCorrect(CalendarEventStatus status, bool hasActual, bool canEnter, bool canCorrect)
    {
        var calendarEvent = new CalendarEvent { Status = status, Actual = hasActual ? 0.3m : null };

        Assert.Equal((canEnter, canCorrect), (ForecastRule.CanEnter(calendarEvent), ForecastRule.CanCorrect(calendarEvent)));
    }
}
