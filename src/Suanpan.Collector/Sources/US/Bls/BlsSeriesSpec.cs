using System.Text.RegularExpressions;
using Suanpan.Collector.Execution;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Collector.Sources.US.Bls;

/// <summary>计算方式（日历设计 §5.1）：API 返回的是水平值，由解析器算出指标的值（BR-043）</summary>
public enum BlsCalculation
{
    /// <summary>不带计算方式：直接取值，如失业率</summary>
    Level,

    /// <summary>chg：较上期变动（本期 − 上期），如非农就业人数变化</summary>
    Change,

    /// <summary>pct：较上期变动率（%），如 CPI 月率</summary>
    PercentChange,

    /// <summary>pct12：较 12 期前的变动率（%），如 CPI 年率</summary>
    PercentChange12,
}

/// <summary>
/// 公布值映射的外部标识（日历设计 §5.1）：<c>{序列 ID}</c> 或 <c>{序列 ID}:{计算方式}</c>，如 <c>CES0000000001:chg</c>。
/// 序列 ID 为大写字母和数字（BLS 的写法）
/// </summary>
public sealed partial record BlsSeriesSpec(string ExternalId, string SeriesId, BlsCalculation Calculation)
{
    public static BlsSeriesSpec Parse(string externalId)
    {
        var parts = externalId.Split(':');
        var seriesId = parts[0];
        if (!SeriesIdFormat().IsMatch(seriesId) || parts.Length > 2)
        {
            throw new FetchParametersException($"外部标识 \"{externalId}\" 应为 序列ID 或 序列ID:计算方式，序列 ID 为大写字母和数字");
        }

        var calculation = parts.Length == 1 ? BlsCalculation.Level : parts[1] switch
        {
            "chg" => BlsCalculation.Change,
            "pct" => BlsCalculation.PercentChange,
            "pct12" => BlsCalculation.PercentChange12,
            _ => throw new FetchParametersException($"外部标识 \"{externalId}\" 的计算方式只能是 chg、pct、pct12"),
        };
        return new BlsSeriesSpec(externalId, seriesId, calculation);
    }

    /// <summary>
    /// 算出的值的单位，与指标的单位比对（ERR-PARSE-006）。变动率都是百分比；取值和变动的单位由序列决定，
    /// 按 BLS 序列编号的规则判断（https://www.bls.gov/help/hlpforma.htm），没有登记的序列返回 null
    /// </summary>
    public IndicatorUnit? Unit => Calculation is BlsCalculation.PercentChange or BlsCalculation.PercentChange12
        ? IndicatorUnit.Percent
        : LevelUnit(SeriesId);

    private static IndicatorUnit? LevelUnit(string seriesId) => seriesId switch
    {
        // CES 就业统计：数据类型 01 为全部雇员（千人）
        _ when seriesId.StartsWith("CE", StringComparison.Ordinal) && seriesId.EndsWith("01", StringComparison.Ordinal) => IndicatorUnit.ThousandPersons,
        // CPS 失业率：LNS14 / LNU04 开头
        _ when seriesId.StartsWith("LNS14", StringComparison.Ordinal) || seriesId.StartsWith("LNU04", StringComparison.Ordinal) => IndicatorUnit.Percent,
        // JOLTS：职位空缺数（JOL，千个）；比率（结尾 R）为百分比
        _ when seriesId.StartsWith("JT", StringComparison.Ordinal) && seriesId.EndsWith("JOL", StringComparison.Ordinal) => IndicatorUnit.ThousandUnits,
        _ when seriesId.StartsWith("JT", StringComparison.Ordinal) && seriesId.EndsWith('R') => IndicatorUnit.Percent,
        // CPI、PPI：指数
        _ when seriesId.StartsWith("CU", StringComparison.Ordinal) || seriesId.StartsWith("WP", StringComparison.Ordinal) => IndicatorUnit.Index,
        _ => null,
    };

    [GeneratedRegex(@"^[A-Z0-9]{5,30}\z")]
    private static partial Regex SeriesIdFormat();
}
