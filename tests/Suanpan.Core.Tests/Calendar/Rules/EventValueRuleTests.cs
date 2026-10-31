using System.Globalization;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Reference.Enums;
using Xunit;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>人工录入的数值格式（BR-058，ERR-FC-001）</summary>
public class EventValueRuleTests
{
    [Theory]
    [InlineData("0.3", null, IndicatorValueType.Value, 1, true)]
    [InlineData("0.30", null, IndicatorValueType.Value, 1, true)]       // 末尾的 0 不算多出的小数位
    [InlineData("0.35", null, IndicatorValueType.Value, 1, false)]      // 小数位超出，不四舍五入
    [InlineData("119.5", null, IndicatorValueType.Value, 0, false)]
    [InlineData("0.3", "0.4", IndicatorValueType.Value, 1, false)]      // 数值型不填上限
    [InlineData("4.25", "4.50", IndicatorValueType.Range, 2, true)]
    [InlineData("4.25", null, IndicatorValueType.Range, 2, false)]      // 区间型缺上限
    [InlineData("4.50", "4.25", IndicatorValueType.Range, 2, false)]    // 上限小于下限
    [InlineData("1000000000000", null, IndicatorValueType.Value, 0, false)]
    public void Validate(string value, string? upper, IndicatorValueType valueType, short decimals, bool valid)
        => Assert.Equal(valid, EventValueRule.Validate(Parse(value), upper is null ? null : Parse(upper), valueType, decimals) is null);

    private static decimal Parse(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
}
