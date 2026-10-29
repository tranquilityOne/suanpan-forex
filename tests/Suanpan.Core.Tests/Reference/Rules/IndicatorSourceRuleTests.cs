using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Reference.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Reference.Rules;

public class IndicatorSourceRuleTests
{
    /// <summary>公布值映射带优先级，排期映射不设优先级</summary>
    [Theory]
    [InlineData(IndicatorSourcePurpose.Release, (short)1)]
    [InlineData(IndicatorSourcePurpose.Schedule, null)]
    public void Validate_AcceptsValidMapping(IndicatorSourcePurpose purpose, short? priority)
    {
        var mapping = Valid();
        mapping.Purpose = purpose;
        mapping.Priority = priority;

        Assert.Null(IndicatorSourceRule.Validate(mapping));
    }

    /// <summary>BR-034：外部标识必填；公布值映射的优先级必填且从 1 开始（1 为主源）；排期映射不设优先级</summary>
    [Theory]
    [InlineData(" ", IndicatorSourcePurpose.Release, (short)1, "ExternalId")]
    [InlineData("CES0000000001", IndicatorSourcePurpose.Release, (short)0, "Priority")]
    [InlineData("CES0000000001", IndicatorSourcePurpose.Release, null, "Priority")]
    [InlineData("empsit", IndicatorSourcePurpose.Schedule, (short)1, "Priority")]
    public void Validate_ReportsTheInvalidField(string externalId, IndicatorSourcePurpose purpose, short? priority, string field)
    {
        var mapping = Valid();
        mapping.ExternalId = externalId;
        mapping.Purpose = purpose;
        mapping.Priority = priority;

        Assert.Equal(field, IndicatorSourceRule.Validate(mapping)?.Field);
    }

    /// <summary>排期映射用"日历排期"的解析器，公布值映射用"公布值"的解析器</summary>
    [Theory]
    [InlineData(IndicatorSourcePurpose.Schedule, CollectTargetType.Calendar)]
    [InlineData(IndicatorSourcePurpose.Release, CollectTargetType.Release)]
    public void ParserTargetType_FollowsPurpose(IndicatorSourcePurpose purpose, CollectTargetType expected)
        => Assert.Equal(expected, IndicatorSourceRule.ParserTargetType(purpose));

    [Theory]
    [InlineData((short)1, "主源")]
    [InlineData((short)3, "备源 2")]
    public void PriorityName_MainThenNumberedBackups(short priority, string expected)
        => Assert.Equal(expected, IndicatorSourceRule.PriorityName(priority));

    private static IndicatorSource Valid() => new()
    {
        IndicatorCode = "US.NFP",
        SourceId = 1,
        Purpose = IndicatorSourcePurpose.Release,
        ExternalId = "CES0000000001",
        ParserKey = "us.bls.release",
        Priority = IndicatorSourceRule.MainPriority,
    };
}
