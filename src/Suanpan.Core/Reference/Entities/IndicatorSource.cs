using Suanpan.Core.Common.Auditing;

namespace Suanpan.Core.Reference.Entities;

/// <summary>
/// 指标与数据源的映射（ref_indicator_source，基础数据设计 §2.1，BR-034）：一个指标可以有主源和备源，
/// 主源失败时由采集模块改用备源。同一指标对同一数据源只映射一次，优先级不重复
/// </summary>
public class IndicatorSource : AuditedEntity
{
    public long Id { get; set; }

    public string IndicatorCode { get; set; } = string.Empty;

    /// <summary>数据源 id（col_source）</summary>
    public long SourceId { get; set; }

    /// <summary>数据源中的标识，如 BLS 的序列 ID</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>解析器标识，取自解析器目录（BR-012）</summary>
    public string ParserKey { get; set; } = string.Empty;

    /// <summary>1 为主源，2 起为备源，数字越小越优先</summary>
    public short Priority { get; set; }
}
