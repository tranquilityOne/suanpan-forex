using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 日历事件的字段锁定（BR-060 ~ BR-063，日历设计 §3.3）。可锁定的字段见 <see cref="EventField"/>：
/// 人工保存时由服务加锁（BR-061）；自动流程遇到锁定字段时不覆盖，值不同时写锁定冲突（BR-062）；解锁后下一次采集照常更新（BR-063）
/// </summary>
public static class EventLockRule
{
    public static bool IsLocked(IEnumerable<EventFieldLock> locks, EventField field) => locks.Any(l => l.Field == field);

    /// <summary>加锁；字段已经锁定时换成这一次的锁定人、时间和原因</summary>
    public static void Lock(List<EventFieldLock> locks, EventField field, string lockedBy, DateTime lockedAt, string reason)
    {
        locks.RemoveAll(l => l.Field == field);
        locks.Add(new EventFieldLock { Field = field, LockedBy = lockedBy, LockedAt = lockedAt, Reason = reason });
    }

    /// <summary>解锁；字段原本没有锁定时返回 false</summary>
    public static bool Unlock(List<EventFieldLock> locks, EventField field) => locks.RemoveAll(l => l.Field == field) > 0;
}
