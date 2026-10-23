using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Reference.Indicators.Models;

/// <summary>
/// 新增或修改指标。修改时 Code 只用来定位，编码和所属经济体都不会被改（BR-029）；
/// 影响方向变化时 Reason 必填（BR-031），由操作日志记录。说明两种语言都没填时不保存说明
/// </summary>
public sealed record IndicatorInput
{
    public required string Code { get; init; }

    public required string EconomyCode { get; init; }

    public required string CurrencyCode { get; init; }

    public required string NameZh { get; init; }

    public required string NameEn { get; init; }

    public required string ShortNameZh { get; init; }

    public required string ShortNameEn { get; init; }

    public required IndicatorCategory Category { get; init; }

    public required IndicatorValueType ValueType { get; init; }

    public required IndicatorUnit Unit { get; init; }

    public required short Decimals { get; init; }

    public required ReleaseFrequency Frequency { get; init; }

    public required Importance Importance { get; init; }

    public required ImpactDirection Direction { get; init; }

    public required decimal FlatTolerance { get; init; }

    public decimal? RangeMin { get; init; }

    public decimal? RangeMax { get; init; }

    public decimal? SigmaN { get; init; }

    public required string AgencyZh { get; init; }

    public required string AgencyEn { get; init; }

    public required TimeOnly DefaultReleaseTime { get; init; }

    public string? DescriptionZh { get; init; }

    public string? DescriptionEn { get; init; }

    /// <summary>修改理由：影响方向变化时必填</summary>
    public string? Reason { get; init; }
}
