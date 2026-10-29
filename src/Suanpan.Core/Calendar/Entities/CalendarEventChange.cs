using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Entities;

/// <summary>
/// 日历事件的变更历史（cal_event_change，日历设计 §2.1）：只追加，永久保留（PRD §8.5）；改期历史即种类为"改期"的记录（BR-039）
/// </summary>
public class CalendarEventChange
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public EventChangeKind Kind { get; set; }

    /// <summary>旧值（jsonb）：改期为 {"at": UTC, "local": 当地时间, "precision": 精度}，状态为状态值，数值为数值字符串</summary>
    public string? OldValue { get; set; }

    /// <summary>新值（jsonb），写法同 <see cref="OldValue"/></summary>
    public string? NewValue { get; set; }

    public ChangeActorKind ActorKind { get; set; }

    /// <summary>采集时的数据源</summary>
    public long? SourceId { get; set; }

    /// <summary>采集时的运行记录</summary>
    public long? RunId { get; set; }

    /// <summary>登录名或程序名</summary>
    public string ChangedBy { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; }

    /// <summary>人工修改的原因（BR-050）；自动流程写说明，如"从官方日程中消失"</summary>
    public string? Reason { get; set; }
}
