using Suanpan.Core.Common.Auditing;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Core.Reference.Entities;

/// <summary>
/// 指标与数据源的映射（ref_indicator_source，基础数据设计 §2.1、§3.8，BR-034）。用途分公布值和排期：
/// 公布值映射有主源和备源，主源失败时由采集模块改用备源；排期映射用官方日程中的发布标识匹配指标（日历设计 §4.2）。
/// 同一指标对同一数据源每种用途只映射一次，公布值映射的优先级不重复
/// </summary>
public class IndicatorSource : AuditedEntity
{
    public long Id { get; set; }

    public string IndicatorCode { get; set; } = string.Empty;

    /// <summary>数据源 id（col_source）</summary>
    public long SourceId { get; set; }

    /// <summary>用途；创建后不能修改</summary>
    public IndicatorSourcePurpose Purpose { get; set; } = IndicatorSourcePurpose.Release;

    /// <summary>数据源中的标识：公布值映射为序列 ID 等，排期映射为发布标识（如 BLS 的 empsit）</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>解析器标识，取自解析器目录（BR-012）；目标数据类型与用途一致</summary>
    public string ParserKey { get; set; } = string.Empty;

    /// <summary>公布值映射：1 为主源，2 起为备源，数字越小越优先；排期映射为空</summary>
    public short? Priority { get; set; }
}
