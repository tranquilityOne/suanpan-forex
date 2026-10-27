using Suanpan.Core.Flash.Enums;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Core.Flash.Rules;

/// <summary>
/// 数据公布快讯的利多利空判定（BR-074、BR-075，PRD DEC-011：只按"预期偏差 + 指标影响方向"机械判断）：
/// <list type="number">
/// <item>有预期值时：偏差 = 公布值 − 预期值。偏差的绝对值 ≤ 持平容差为"符合预期"，不论影响方向；超出容差时，
/// 越高越利好的指标偏差 > 0 为利多、&lt; 0 为利空，越高越利空的相反，无方向的不给结论</item>
/// <item>没有预期值时：与前值比较（有前值修正时用修正值），差的绝对值 ≤ 持平容差为"持平"，否则为"上升"或"下降"，
/// 不出现利多利空；前值也没有时不给结论</item>
/// </list>
/// 预期值只传人工录入（市场共识）的；其他来源的预期值按没有预期值处理，调用方不要传入（BR-074）。
/// 区间型指标（如利率目标区间）比较哪一个值，由调用方（央行决议快讯 FR-015）决定后传入
/// </summary>
public static class FlashConclusionRule
{
    /// <param name="actual">公布值</param>
    /// <param name="consensus">人工录入的预期值（市场共识）；没有时为空</param>
    /// <param name="previous">前值</param>
    /// <param name="previousRevised">前值修正（BR-044）；有时代替前值参与比较</param>
    /// <param name="direction">指标的影响方向（BR-031）</param>
    /// <param name="flatTolerance">指标的持平容差，不小于 0（IndicatorRule 保证）</param>
    public static FlashConclusion Decide(
        decimal actual, decimal? consensus, decimal? previous, decimal? previousRevised, ImpactDirection direction, decimal flatTolerance)
    {
        if (consensus is { } expected)
        {
            var deviation = actual - expected;
            if (Math.Abs(deviation) <= flatTolerance)
            {
                return FlashConclusion.InLine;
            }

            return direction switch
            {
                ImpactDirection.HigherIsBullish => deviation > 0 ? FlashConclusion.Bullish : FlashConclusion.Bearish,
                ImpactDirection.HigherIsBearish => deviation > 0 ? FlashConclusion.Bearish : FlashConclusion.Bullish,
                _ => FlashConclusion.None,
            };
        }

        if ((previousRevised ?? previous) is not { } prior)
        {
            return FlashConclusion.None;
        }

        var change = actual - prior;
        if (Math.Abs(change) <= flatTolerance)
        {
            return FlashConclusion.FlatWithPrevious;
        }

        return change > 0 ? FlashConclusion.AbovePrevious : FlashConclusion.BelowPrevious;
    }

    /// <summary>含利多利空结论的快讯带"不构成投资建议"标记，前台据此展示免责声明（BR-080）</summary>
    public static bool NeedsNotAdviceFlag(FlashConclusion conclusion)
        => conclusion is FlashConclusion.Bullish or FlashConclusion.Bearish;
}
