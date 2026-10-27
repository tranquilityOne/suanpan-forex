using Suanpan.Core.Flash.Enums;
using Suanpan.Core.Flash.Rules;
using Suanpan.Core.Reference.Enums;
using Xunit;

namespace Suanpan.Core.Tests.Flash.Rules;

/// <summary>利多利空判定（BR-074、BR-075，FR-014 验收第 1、2 条）：每个分支和容差边界一个样例</summary>
public class FlashConclusionRuleTests
{
    /// <summary>有预期值：按偏差和影响方向判断；容差内为符合预期，不论方向（含无方向）；有预期值时不看前值</summary>
    [Theory]
    [MemberData(nameof(WithConsensus))]
    public void Decide_WithConsensus(decimal actual, decimal consensus, ImpactDirection direction, decimal tolerance, FlashConclusion expected)
        => Assert.Equal(expected, FlashConclusionRule.Decide(actual, consensus, previous: 1000m, previousRevised: null, direction, tolerance));

    public static TheoryData<decimal, decimal, ImpactDirection, decimal, FlashConclusion> WithConsensus => new()
    {
        { 250m, 180m, ImpactDirection.HigherIsBullish, 0m, FlashConclusion.Bullish },   // 非农高于预期
        { 150m, 180m, ImpactDirection.HigherIsBullish, 0m, FlashConclusion.Bearish },
        { 4.3m, 4.1m, ImpactDirection.HigherIsBearish, 0m, FlashConclusion.Bearish },   // 失业率高于预期
        { 3.9m, 4.1m, ImpactDirection.HigherIsBearish, 0m, FlashConclusion.Bullish },
        { 52m, 50m, ImpactDirection.Neutral, 0m, FlashConclusion.None },                // 无方向偏离预期不给结论
        { 50m, 50m, ImpactDirection.Neutral, 0m, FlashConclusion.InLine },              // 无方向也可以符合预期
        { 3.2m, 3.1m, ImpactDirection.HigherIsBullish, 0.1m, FlashConclusion.InLine },  // 偏差等于容差算符合预期
        { 3.21m, 3.1m, ImpactDirection.HigherIsBullish, 0.1m, FlashConclusion.Bullish }, // 刚超出容差
    };

    /// <summary>没有预期值：与前值（有修正时用修正值）比较，容差内为持平；没有前值时无结论</summary>
    [Theory]
    [MemberData(nameof(WithoutConsensus))]
    public void Decide_WithoutConsensus(decimal actual, decimal? previous, decimal? previousRevised, decimal tolerance, FlashConclusion expected)
        => Assert.Equal(expected, FlashConclusionRule.Decide(actual, consensus: null, previous, previousRevised, ImpactDirection.HigherIsBullish, tolerance));

    public static TheoryData<decimal, decimal?, decimal?, decimal, FlashConclusion> WithoutConsensus => new()
    {
        { 110m, 100m, null, 0m, FlashConclusion.AbovePrevious },
        { 90m, 100m, null, 0m, FlashConclusion.BelowPrevious },
        { 100.5m, 100m, null, 0.5m, FlashConclusion.FlatWithPrevious },  // 与前值的差等于容差算持平
        { 105m, 100m, 110m, 0m, FlashConclusion.BelowPrevious },         // 用修正后的前值 110 比较
        { 105m, null, null, 0m, FlashConclusion.None },
    };

    /// <summary>FR-014 验收第 2 条：没有预期值时，不论影响方向都不出现利多利空</summary>
    [Theory]
    [InlineData(ImpactDirection.HigherIsBullish)]
    [InlineData(ImpactDirection.HigherIsBearish)]
    [InlineData(ImpactDirection.Neutral)]
    public void Decide_WithoutConsensus_NeverBullishOrBearish(ImpactDirection direction)
        => Assert.Equal(FlashConclusion.AbovePrevious, FlashConclusionRule.Decide(110m, null, 100m, null, direction, 0m));

    /// <summary>BR-080：只有利多、利空的快讯带"不构成投资建议"标记</summary>
    [Fact]
    public void NeedsNotAdviceFlag_OnlyBullishAndBearish()
        => Assert.Equal(
            [FlashConclusion.Bullish, FlashConclusion.Bearish],
            Enum.GetValues<FlashConclusion>().Where(FlashConclusionRule.NeedsNotAdviceFlag));
}
