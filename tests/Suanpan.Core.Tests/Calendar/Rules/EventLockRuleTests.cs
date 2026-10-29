using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>字段锁定（BR-061、BR-063）：同一字段只有一条锁定信息，再次锁定时换成新的锁定人和原因</summary>
public class EventLockRuleTests
{
    private static readonly DateTime At = new(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Lock_ReplacesTheExistingLockOfTheSameField()
    {
        var locks = new List<EventFieldLock>();
        EventLockRule.Lock(locks, EventField.Actual, "alice", At, "官方更正");
        EventLockRule.Lock(locks, EventField.Forecast, "alice", At, "录入预期值");
        EventLockRule.Lock(locks, EventField.Actual, "bob", At.AddHours(1), "再次更正");

        Assert.Equal([EventField.Forecast, EventField.Actual], locks.Select(l => l.Field));
        Assert.Equal(("bob", "再次更正"), (locks[1].LockedBy, locks[1].Reason));
        Assert.True(EventLockRule.IsLocked(locks, EventField.Actual));
        Assert.False(EventLockRule.IsLocked(locks, EventField.Previous));
    }

    [Fact]
    public void Unlock_RemovesOnlyThatField()
    {
        var locks = new List<EventFieldLock>();
        EventLockRule.Lock(locks, EventField.Actual, "alice", At, "官方更正");
        EventLockRule.Lock(locks, EventField.Status, "alice", At, "人工确认");

        Assert.True(EventLockRule.Unlock(locks, EventField.Actual));
        Assert.False(EventLockRule.Unlock(locks, EventField.Actual));
        Assert.Equal([EventField.Status], locks.Select(l => l.Field));
    }
}
