using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Ops.Enums;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Services.Calendar.Jobs;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Calendar.Forecasts;

/// <summary>
/// 预期值的提醒（日历设计 §3.8，BR-056）：高重要性、未公布、预期值为空、精确到分钟且排期在未来 24 小时内的事件告警一次，
/// 再次执行不重复提醒
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class ForecastReminderJobTests(PostgresFixture postgres)
{
    /// <summary>TestData.Event 的排期为 2026-11-06 13:30 UTC，在"现在"之后的 23.5 小时</summary>
    private static readonly DateTime Now = new(2026, 11, 5, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RemindsDueEventsOnce()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var high = await AddIndicatorAsync(db, Importance.High);
        var due = TestData.Event(high, "2026-10");
        var entered = TestData.Event(high, "2026-09");
        entered.Forecast = 0.3m;
        var dateOnly = TestData.Event(high, "2026-08");
        dateOnly.TimePrecision = TimePrecision.DateOnly;
        var later = TestData.Event(high, "2026-11");
        later.ScheduledAt = Now.AddHours(25);
        db.CalendarEvents.AddRange(due, entered, dateOnly, later, TestData.Event(await AddIndicatorAsync(db, Importance.Medium)));
        await db.SaveChangesAsync(Ct);
        var cache = new FakeRedisCache();
        var alerts = new RecordingAlertService();

        await Create(db, cache, alerts).RunAsync(Ct);
        await Create(db, cache, alerts).RunAsync(Ct);

        Assert.Equal([(AlertLevel.Important, "预期值录入")], alerts.Raised);
        Assert.False(await cache.SetIfNotExistsAsync(ForecastReminderJob.RemindedKey(due.Id), true, TimeSpan.FromHours(1)));
    }

    private static ForecastReminderJob Create(SuanpanDbContext db, FakeRedisCache cache, RecordingAlertService alerts)
        => new(db, cache, alerts, new StubDistributedLock(), new FixedTimeProvider(new DateTimeOffset(Now)), NullLogger<ForecastReminderJob>.Instance);

    private static async Task<string> AddIndicatorAsync(SuanpanDbContext db, Importance importance)
    {
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code) with { Importance = importance }, Ct);
        return code;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
