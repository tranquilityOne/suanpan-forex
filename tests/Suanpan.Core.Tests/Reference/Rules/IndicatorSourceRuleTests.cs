using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Reference.Rules;

public class IndicatorSourceRuleTests
{
    [Fact]
    public void Validate_AcceptsMainSource()
        => Assert.Null(IndicatorSourceRule.Validate(Valid()));

    /// <summary>BR-034：外部标识必填；优先级从 1 开始（1 为主源）</summary>
    [Theory]
    [InlineData(" ", (short)1, "ExternalId")]
    [InlineData("CES0000000001", (short)0, "Priority")]
    public void Validate_ReportsTheInvalidField(string externalId, short priority, string field)
    {
        var mapping = Valid();
        mapping.ExternalId = externalId;
        mapping.Priority = priority;

        Assert.Equal(field, IndicatorSourceRule.Validate(mapping)?.Field);
    }

    [Theory]
    [InlineData((short)1, "主源")]
    [InlineData((short)3, "备源 2")]
    public void PriorityName_MainThenNumberedBackups(short priority, string expected)
        => Assert.Equal(expected, IndicatorSourceRule.PriorityName(priority));

    private static IndicatorSource Valid() => new()
    {
        IndicatorCode = "US.NFP",
        SourceId = 1,
        ExternalId = "CES0000000001",
        ParserKey = "us.bls.release",
        Priority = IndicatorSourceRule.MainPriority,
    };
}
