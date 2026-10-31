using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Reference.IndicatorSources.Models;

/// <summary>新增或修改指标与数据源的映射（基础数据设计 §3.8）；修改时指标和用途不能改，IndicatorCode、Purpose 不使用</summary>
public sealed record IndicatorSourceInput
{
    public required string IndicatorCode { get; init; }

    public required long SourceId { get; init; }

    public required IndicatorSourcePurpose Purpose { get; init; }

    public required string ExternalId { get; init; }

    public required string ParserKey { get; init; }

    /// <summary>公布值映射：1 为主源，2 起为备源；排期映射为空</summary>
    public required short? Priority { get; init; }

    /// <summary>盯盘提前秒数、窗口分钟（BR-013，只对公布值映射有效）；为空时用系统配置的默认值</summary>
    public short? WatchLeadSeconds { get; init; }

    public short? WatchWindowMinutes { get; init; }
}
