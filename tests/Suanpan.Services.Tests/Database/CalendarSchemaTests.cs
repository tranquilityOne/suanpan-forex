using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Database;

/// <summary>
/// 日历事件表的映射（日历设计 §2.1）：锁定信息存为 jsonb 数组、机构当地时间不带时区，读回来不变；
/// 事件被别人改过（row_version 变了）时保存失败，人工编辑据此提示刷新（ERR-EVT-009）
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CalendarSchemaTests(PostgresFixture postgres)
{
    private static readonly DateTime LockedAt = new(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CalendarEvent_RoundTripsLocksAndLocalTime()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var saved = TestData.Event(TestData.IndicatorCode());
        EventLockRule.Lock(saved.LockedFields, EventField.Actual, "editor", LockedAt, "官方更正");
        EventLockRule.Lock(saved.LockedFields, EventField.Status, "editor", LockedAt, "人工确认");
        db.CalendarEvents.Add(saved);
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();

        var loaded = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == saved.Id, Ct);

        Assert.Equal(
            [(EventField.Actual, "editor", LockedAt, "官方更正"), (EventField.Status, "editor", LockedAt, "人工确认")],
            loaded.LockedFields.Select(l => (l.Field, l.LockedBy, l.LockedAt, l.Reason)));
        Assert.Equal((saved.ScheduledLocal, DateTimeKind.Unspecified), (loaded.ScheduledLocal, loaded.ScheduledLocal!.Value.Kind));
        Assert.Equal(saved.ScheduledAt, loaded.ScheduledAt);
    }

    [Fact]
    public async Task CalendarEvent_RejectsStaleRowVersion()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var calendarEvent = TestData.Event(TestData.IndicatorCode());
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(Ct);

        // 另一个写入方先改了这个事件（如采集入库），版本号加一
        await db.CalendarEvents.Where(e => e.Id == calendarEvent.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.RowVersion, e => e.RowVersion + 1), Ct);
        calendarEvent.Actual = 256;
        calendarEvent.RowVersion++;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync(Ct));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
