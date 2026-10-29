using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Calendar.Rules;

public class CalendarDisplayNamesTests
{
    /// <summary>财经日历的每个枚举值都有中文显示名：新增枚举或取值时漏登记，后台会显示英文枚举名，只能靠这里发现</summary>
    [Fact]
    public void EveryCalendarEnumValueHasAName()
    {
        var values = typeof(CalendarEventStatus).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace == typeof(CalendarEventStatus).Namespace)
            .SelectMany(t => Enum.GetValues(t).Cast<Enum>())
            .ToList();

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.True(CalendarDisplayNames.Contains(value), $"{value.GetType().Name}.{value} 没有显示名"));
    }
}
