using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Collect.Rules;

public class CollectDisplayNamesTests
{
    /// <summary>数据采集的每个枚举值都有中文显示名：新增枚举或取值时漏登记，后台会显示英文枚举名，只能靠这里发现</summary>
    [Fact]
    public void EveryCollectEnumValueHasAName()
    {
        var values = typeof(DataSourceType).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace == typeof(DataSourceType).Namespace)
            .SelectMany(t => Enum.GetValues(t).Cast<Enum>())
            .ToList();

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.True(CollectDisplayNames.Contains(value), $"{value.GetType().Name}.{value} 没有显示名"));
    }
}
