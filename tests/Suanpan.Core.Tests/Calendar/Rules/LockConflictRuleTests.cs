using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>锁定冲突的处理（日历设计 §3.9，§12 第 21 项）</summary>
public class LockConflictRuleTests
{
    private static readonly DateTime LockedAt = new(2026, 11, 6, 14, 0, 0, DateTimeKind.Utc);

    private static readonly EventFieldLock ActualLock = new() { Field = EventField.Actual, LockedBy = "editor", LockedAt = LockedAt, Reason = "人工录入" };

    [Theory]
    [InlineData(EventField.Actual, true)]
    [InlineData(EventField.Previous, true)]
    [InlineData(EventField.PreviousRevised, true)]
    [InlineData(EventField.Forecast, false)]
    [InlineData(EventField.Status, false)]
    public void CanAdopt(EventField field, bool expected) => Assert.Equal(expected, LockConflictRule.CanAdopt(field));

    /// <summary>
    /// 当前这把锁下保持锁定过的同一个采集值（连同上限）不再产生冲突；采集值、上限变了，处理方式不是保持锁定，
    /// 或者处理在重新锁定之前的，都照常产生
    /// </summary>
    [Theory]
    [InlineData("\"0.3\"", null, LockConflictResolution.KeepLocked, 0, true)]
    [InlineData("\"0.4\"", null, LockConflictResolution.KeepLocked, 0, false)]
    [InlineData("\"0.3\"", 0.5, LockConflictResolution.KeepLocked, 0, false)]
    [InlineData("\"0.3\"", null, LockConflictResolution.Unlocked, 0, false)]
    [InlineData("\"0.3\"", null, LockConflictResolution.KeepLocked, -1, false)]
    public void WasKept(string collected, double? upper, LockConflictResolution resolution, int resolvedAfterLockHours, bool expected)
    {
        var resolved = new LockConflict
        {
            Field = EventField.Actual, CollectedValue = "\"0.3\"", Status = LockConflictStatus.Resolved, Resolution = resolution,
            ResolvedAt = LockedAt.AddHours(resolvedAfterLockHours),
        };

        Assert.Equal(expected, LockConflictRule.WasKept([resolved], ActualLock, collected, (decimal?)upper));
    }
}
