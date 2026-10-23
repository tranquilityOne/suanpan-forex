using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Reference.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Reference.Rules;

public class ReferenceDisplayNamesTests
{
    /// <summary>
    /// 基础数据的每个枚举值都有中英文显示名：新增枚举或取值时漏登记，后台和快讯会显示英文枚举名，只能靠这里发现
    /// </summary>
    [Fact]
    public void EveryReferenceEnumValueHasBothNames()
    {
        var values = typeof(IndicatorCategory).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace == typeof(IndicatorCategory).Namespace)
            .SelectMany(t => Enum.GetValues(t).Cast<Enum>())
            .ToList();

        Assert.NotEmpty(values);
        Assert.All(values, value =>
        {
            Assert.True(ReferenceDisplayNames.Contains(value), $"{value.GetType().Name}.{value} 没有显示名");
            var name = ReferenceDisplayNames.Get(value);
            Assert.False(string.IsNullOrWhiteSpace(name.Zh) || string.IsNullOrWhiteSpace(name.En), $"{value.GetType().Name}.{value} 缺少中文或英文");
        });
    }
}
