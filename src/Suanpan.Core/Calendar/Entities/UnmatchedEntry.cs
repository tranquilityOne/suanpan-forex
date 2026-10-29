using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Entities;

/// <summary>
/// 未匹配条目（cal_unmatched_entry，ERR-EVT-003、BR-156，日历设计 §4.5）：官方日程中没有排期映射、也没有被忽略的条目。
/// 数据源 + 发布标识 + 参考期唯一；只保留未来的条目
/// </summary>
public class UnmatchedEntry
{
    public long Id { get; set; }

    public long SourceId { get; set; }

    /// <summary>发布标识，如 BLS 的 empsit</summary>
    public string ReleaseKey { get; set; } = string.Empty;

    /// <summary>发布名称，如 Employment Situation</summary>
    public string ReleaseName { get; set; } = string.Empty;

    public string ReferencePeriod { get; set; } = string.Empty;

    public DateOnly? LocalDate { get; set; }

    public TimeOnly? LocalTime { get; set; }

    public TimePrecision TimePrecision { get; set; }

    public DateTime FirstSeenAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public long LastRunId { get; set; }
}
