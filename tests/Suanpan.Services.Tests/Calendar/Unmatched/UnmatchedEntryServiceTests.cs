using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Schedules;
using Suanpan.Services.Calendar.Unmatched;
using Suanpan.Services.Calendar.Unmatched.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Calendar.Unmatched;

/// <summary>
/// 未匹配条目的忽略、恢复、重新匹配（BR-156，日历设计 §4.5）。现在是 UTC 2026-10-29 12:00；数据源属于美国（America/New_York）。
/// 排期写入事件的分支在 CalendarScheduleIngestorTests 中测试，这里测重新匹配接上了 ScheduleApplier。断言只看本测试建的数据源
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class UnmatchedEntryServiceTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 按发布分组，最近要发布的组在前，带数据源所属经济体的时区；忽略后这个发布的条目被删除，已忽略列表记下发布名称和忽略人；
    /// 恢复删除忽略记录
    /// </summary>
    [Fact]
    public async Task Ignore_RemovesEntriesAndRestoreDeletesTheRecord()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (service, _) = Create(db);
        var source = await AddSourceAsync(db);
        db.UnmatchedEntries.AddRange(
            Entry(source.Id, "realer", "2026-10", 11, 13), Entry(source.Id, "realer", "2026-11", 12, 10), Entry(source.Id, "prod", "2026-Q3", 11, 5));
        await db.SaveChangesAsync(Ct);

        var groups = (await service.QueryAsync(new UnmatchedEntryQuery(source.Id), Ct)).Items;
        var ignore = await service.IgnoreAsync(source.Id, "realer", Ct);
        var ignored = Assert.Single((await service.QueryIgnoredAsync(new IgnoredReleaseQuery(source.Id), Ct)).Items);
        var restore = await service.RestoreAsync(ignored.Id, Ct);

        Assert.Equal([("prod", 1), ("realer", 2)], groups.Select(g => (g.ReleaseKey, g.Entries.Count)));
        Assert.Equal("America/New_York", groups[0].TimeZone);
        Assert.Equal((true, true), (ignore.Success, restore.Success));
        Assert.Equal(("Release realer", "test"), (ignored.ReleaseName, ignored.CreatedBy));
        Assert.Equal(["prod"], await db.UnmatchedEntries.Where(u => u.SourceId == source.Id).Select(u => u.ReleaseKey).ToListAsync(Ct));
        Assert.False(await db.IgnoredReleases.AnyAsync(i => i.SourceId == source.Id, Ct));
    }

    /// <summary>
    /// 新增映射后重新匹配：未来的条目建成事件（发布批次、维护排期的数据源、夏令时结束后的换算），已经过去的只删除不建；
    /// 没有映射的条目保留
    /// </summary>
    [Fact]
    public async Task Rematch_CreatesEventsForNewlyMappedReleases()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (service, _) = Create(db);
        var source = await AddSourceAsync(db);
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code), Ct);
        db.IndicatorSources.Add(new IndicatorSource
        {
            IndicatorCode = code, SourceId = source.Id, Purpose = IndicatorSourcePurpose.Schedule, ExternalId = "prod", ParserKey = "us.test.schedule",
        });
        db.UnmatchedEntries.AddRange(
            Entry(source.Id, "prod", "2026-10", 11, 13), Entry(source.Id, "prod", "2026-08", 10, 2), Entry(source.Id, "other", "2026-10", 11, 20));
        await db.SaveChangesAsync(Ct);

        var matched = await service.RematchAsync(Ct);

        Assert.Equal(2, matched);
        var created = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.IndicatorCode == code, Ct);
        Assert.Equal(
            ("2026-10", (DateTime?)new DateTime(2026, 11, 13, 13, 30, 0, DateTimeKind.Utc), (long?)source.Id, "prod", $"{source.Id}:prod:2026-11-13"),
            (created.ReferencePeriod, created.ScheduledAt, created.ScheduleSourceId, created.ScheduleReleaseKey, created.ReleaseBatchId));
        Assert.Equal(["other"], await db.UnmatchedEntries.Where(u => u.SourceId == source.Id).Select(u => u.ReleaseKey).ToListAsync(Ct));
    }

    private static (UnmatchedEntryService Service, RecordingEventBus Bus) Create(SuanpanDbContext db)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(Now));
        var bus = new RecordingEventBus();
        var applier = new ScheduleApplier(db, new FixedAuditActorProvider("editor"), time, NullLogger<ScheduleApplier>.Instance);
        return (new UnmatchedEntryService(db, applier, bus, time, NullLogger<UnmatchedEntryService>.Instance), bus);
    }

    /// <summary>属于美国的数据源</summary>
    private static async Task<DataSource> AddSourceAsync(SuanpanDbContext db)
    {
        var source = await TestData.AddDataSourceAsync(db, Ct);
        source.EconomyCode = "US";
        await db.SaveChangesAsync(Ct);
        return source;
    }

    /// <summary>纽约当地 08:30 发布的一期</summary>
    private static UnmatchedEntry Entry(long sourceId, string releaseKey, string period, int month, int day) => new()
    {
        SourceId = sourceId, ReleaseKey = releaseKey, ReleaseName = $"Release {releaseKey}", ReferencePeriod = period,
        LocalDate = new DateOnly(2026, month, day), LocalTime = new TimeOnly(8, 30), TimePrecision = TimePrecision.Minute,
        FirstSeenAt = Now, LastSeenAt = Now, LastRunId = 1,
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
