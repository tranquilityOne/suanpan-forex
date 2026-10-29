using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Entities;

/// <summary>日历事件中一个字段的锁定信息（cal_event.locked_fields，jsonb 数组，BR-061）：谁、什么时候、为什么锁定</summary>
public sealed class EventFieldLock
{
    public EventField Field { get; set; }

    /// <summary>锁定人的登录名</summary>
    public string LockedBy { get; set; } = string.Empty;

    public DateTime LockedAt { get; set; }

    /// <summary>修改原因（BR-050）</summary>
    public string Reason { get; set; } = string.Empty;
}
