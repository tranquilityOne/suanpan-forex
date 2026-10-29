namespace Suanpan.Core.Calendar.Entities;

/// <summary>
/// 日历事件在各数据源的数值（cal_event_value，日历设计 §2.1）：每个事件每个数据源一行，只保留最新值；系统写入，不带审计字段。
/// 用于事件详情中"各数据源的数值"（FR-009）、待复核时的候选值（BR-051）和主备一致性校验（BR-133）
/// </summary>
public class CalendarEventValue
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public long SourceId { get; set; }

    /// <summary>按指标小数位四舍五入后的值</summary>
    public decimal Value { get; set; }

    public decimal? ValueUpper { get; set; }

    /// <summary>原始字符串（BR-043）</summary>
    public string Raw { get; set; } = string.Empty;

    /// <summary>来源快照</summary>
    public long? SnapshotId { get; set; }

    /// <summary>产生它的运行记录</summary>
    public long RunId { get; set; }

    /// <summary>这个数据源第一次得到当前值的时间；值没变时不更新</summary>
    public DateTime DetectedAt { get; set; }
}
