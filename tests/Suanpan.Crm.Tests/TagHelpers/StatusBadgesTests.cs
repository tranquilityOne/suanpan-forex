using Suanpan.Crm.TagHelpers;
using Xunit;

namespace Suanpan.Crm.Tests.TagHelpers;

public class StatusBadgesTests
{
    /// <summary>登记过的枚举，每个值都要有文字和颜色；枚举新增值时漏登记会被发现（否则页面上显示英文枚举名）</summary>
    [Fact]
    public void EveryValueOfRegisteredEnumsHasBadge()
    {
        var values = StatusBadges.EnumTypes.SelectMany(type => Enum.GetValues(type).Cast<Enum>()).ToList();

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.NotEqual(value.ToString(), StatusBadges.Describe(value).Text));
    }
}
