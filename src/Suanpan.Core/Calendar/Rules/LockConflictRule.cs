using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 锁定冲突的处理（FR-011，BR-062，日历设计 §3.9）：编辑选择保持锁定或解锁并采用采集值；
/// 保持锁定后，同一把锁下再次采集到同一个值不再产生冲突（§12 第 21 项）
/// </summary>
public static class LockConflictRule
{
    /// <summary>能解锁并采用采集值的字段：公布值、前值、前值修正（状态不会被人工锁定，预期值没有自动来源）</summary>
    public static bool CanAdopt(EventField field) => field is EventField.Actual or EventField.Previous or EventField.PreviousRevised;

    /// <summary>
    /// 这个采集值是否已被编辑"保持锁定"过：当前这把锁加上之后（处理时间不早于锁定时间），有处理方式为保持锁定、
    /// 采集值和上限都相同的冲突。重新锁定之后以前的处理不再算数
    /// </summary>
    /// <param name="resolved">这个事件这个字段已处理的冲突</param>
    /// <param name="fieldLock">字段当前的锁定信息</param>
    /// <param name="collectedValue">采集值，写法同 LockConflict.CollectedValue</param>
    /// <param name="collectedUpper">采集值的上限</param>
    public static bool WasKept(IEnumerable<LockConflict> resolved, EventFieldLock fieldLock, string collectedValue, decimal? collectedUpper)
        => resolved.Any(c => c.Field == fieldLock.Field
            && c.Status == LockConflictStatus.Resolved
            && c.Resolution == LockConflictResolution.KeepLocked
            && c.ResolvedAt >= fieldLock.LockedAt
            && c.CollectedValue == collectedValue
            && c.CollectedUpper == collectedUpper);
}
