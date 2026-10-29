using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Entities;

/// <summary>
/// 锁定冲突（cal_lock_conflict，BR-062，日历设计 §2.1）：自动流程遇到锁定字段、采集值与锁定值不同时记录，供编辑处理（FR-011）。
/// 同一事件同一字段只有一条待处理的冲突，再次冲突时更新采集值和最近时间
/// </summary>
public class LockConflict
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public EventField Field { get; set; }

    /// <summary>锁定值（jsonb），写法同 CalendarEventChange 的数值</summary>
    public string LockedValue { get; set; } = string.Empty;

    /// <summary>采集值（jsonb）</summary>
    public string CollectedValue { get; set; } = string.Empty;

    public long SourceId { get; set; }

    public long? SnapshotId { get; set; }

    /// <summary>最近一次产生冲突的运行记录</summary>
    public long RunId { get; set; }

    public DateTime FirstDetectedAt { get; set; }

    public DateTime LastDetectedAt { get; set; }

    public LockConflictStatus Status { get; set; } = LockConflictStatus.Pending;

    public string? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public LockConflictResolution? Resolution { get; set; }
}
