using Suanpan.Core.Sys.Enums;
using Suanpan.Core.Sys.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Sys.Rules;

public class ConfigValueRuleTests
{
    [Theory]
    [InlineData(ConfigValueType.String, " 原样保存 ", " 原样保存 ")]
    [InlineData(ConfigValueType.Int, " 5 ", "5")]
    [InlineData(ConfigValueType.Decimal, "0.25", "0.25")]
    [InlineData(ConfigValueType.Bool, "TRUE", "true")]
    [InlineData(ConfigValueType.Bool, "1", "true")]
    [InlineData(ConfigValueType.Bool, "0", "false")]
    [InlineData(ConfigValueType.Json, " {\"a\":1} ", "{\"a\":1}")]
    public void TryNormalize_AcceptsAndNormalizesValidValues(ConfigValueType type, string value, string expected)
    {
        Assert.True(ConfigValueRule.TryNormalize(type, value, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(ConfigValueType.Int, "1.5")]
    [InlineData(ConfigValueType.Decimal, "0,25")]   // 只接受 InvariantCulture 的小数点
    [InlineData(ConfigValueType.Bool, "yes")]
    [InlineData(ConfigValueType.Json, "{a:1")]
    [InlineData(ConfigValueType.String, null)]
    public void TryNormalize_RejectsValuesOfWrongFormat(ConfigValueType type, string? value)
    {
        Assert.False(ConfigValueRule.TryNormalize(type, value, out _));
    }
}
