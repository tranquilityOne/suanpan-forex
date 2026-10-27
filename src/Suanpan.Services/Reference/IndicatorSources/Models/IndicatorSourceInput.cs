namespace Suanpan.Services.Reference.IndicatorSources.Models;

/// <summary>新增或修改指标与数据源的映射（基础数据设计 §3.8）；修改时指标不能改，IndicatorCode 不使用</summary>
public sealed record IndicatorSourceInput
{
    public required string IndicatorCode { get; init; }

    public required long SourceId { get; init; }

    public required string ExternalId { get; init; }

    public required string ParserKey { get; init; }

    /// <summary>1 为主源，2 起为备源</summary>
    public required short Priority { get; init; }
}
