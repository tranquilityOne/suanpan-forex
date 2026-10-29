using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Records;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Schedules;
using Suanpan.Services.Collect.Ingest;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Messages.Calendar;
using Xunit;

namespace Suanpan.Services.Tests.Calendar.Schedules;

/// <summary>
/// 排期入库（FR-007，日历设计 §4）。现在是 UTC 2026-10-29 12:00；指标都属于美国（America/New_York），月度。
/// 规则的分支在 Core 的 ScheduleRuleTests 等中测试，这里测入库接上了规则以及数据库中的结果。断言只看本测试建的指标和数据源
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CalendarScheduleIngestorTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// FR-007 验收第 1、2 条，BR-156：一个发布标识建出两个指标的事件，带同一发布批次；夏令时结束前后（10 月 30 日为 EDT、
    /// 11 月 6 日为 EST）换算正确；前值取上一期已公布事件的公布值（BR-044）。同一批条目再入库一次，事件数不变、全部跳过
    /// </summary>
    [Fact]
    public async Task Ingest_CreatesEventsOnceAndConvertsLocalTime()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, _, source) = await CreateAsync(db);
        var (nfp, unemp) = (await AddIndicatorAsync(db), await AddIndicatorAsync(db));
        await MapAsync(db, source.Id, "empsit", nfp, unemp);
        var released = TestData.Event(nfp, "2026-08");
        released.Status = CalendarEventStatus.Released;
        released.Actual = 4.1m;
        db.CalendarEvents.Add(released);
        await db.SaveChangesAsync(Ct);
        IReadOnlyList<object> entries = [Entry("empsit", "2026-09", 10, 30), Entry("empsit", "2026-10", 11, 6)];

        var first = await ingestor.IngestAsync(Request(source.Id, entries), Ct);
        var second = await ingestor.IngestAsync(Request(source.Id, entries), Ct);

        Assert.Equal(new IngestResult(4, 0, 0, 0), first);
        Assert.Equal(new IngestResult(0, 0, 4, 0), second);
        var events = await EventsAsync(db, nfp, unemp);
        Assert.Equal(5, events.Count);
        var september = events.Single(e => e.IndicatorCode == nfp && e.ReferencePeriod == "2026-09");
        var october = events.Single(e => e.IndicatorCode == nfp && e.ReferencePeriod == "2026-10");
        Assert.Equal(Utc(10, 30, 12, 30), september.ScheduledAt);
        Assert.Equal(Utc(11, 6, 13, 30), october.ScheduledAt);
        Assert.Equal(
            (CalendarEventStatus.Scheduled, EventOrigin.Collected, (long?)source.Id, "empsit", "America/New_York", (decimal?)4.1m),
            (september.Status, september.Origin, september.ScheduleSourceId, september.ScheduleReleaseKey, september.TimeZone, september.Previous));
        Assert.Null(october.Previous);
        Assert.Equal(
            [$"{source.Id}:empsit:2026-10-30"],
            events.Where(e => e.ReferencePeriod == "2026-09").Select(e => e.ReleaseBatchId).Distinct());
    }

    /// <summary>
    /// FR-007 验收第 3 条、PRD §8.1.3：官方改期后排期更新，有"改期"变更历史，并发出 EventRescheduledEvent；
    /// 已经"延迟"的事件改期后回到"待公布"
    /// </summary>
    [Fact]
    public async Task Ingest_ReschedulesAndReturnsDelayedEventToScheduled()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, bus, source) = await CreateAsync(db);
        var nfp = await AddIndicatorAsync(db);
        await MapAsync(db, source.Id, "empsit", nfp);
        await ingestor.IngestAsync(Request(source.Id, [Entry("empsit", "2026-10", 11, 6)]), Ct);
        await db.CalendarEvents.Where(e => e.IndicatorCode == nfp)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, CalendarEventStatus.Delayed), Ct);

        var result = await ingestor.IngestAsync(Request(source.Id, [Entry("empsit", "2026-10", 11, 20)]), Ct);

        Assert.Equal(new IngestResult(0, 1, 0, 0), result);
        var moved = Assert.Single(await EventsAsync(db, nfp));
        Assert.Equal(((DateTime?)Utc(11, 20, 13, 30), CalendarEventStatus.Scheduled, 1), (moved.ScheduledAt, moved.Status, moved.RowVersion));
        var changes = await db.CalendarEventChanges.Where(c => c.EventId == moved.Id).Select(c => c.Kind).ToListAsync(Ct);
        Assert.Equal([EventChangeKind.Rescheduled, EventChangeKind.Status], changes.Order());
        var message = Assert.Single(bus.Published.OfType<EventRescheduledEvent>());
        Assert.Equal((moved.Id, (DateTime?)Utc(11, 6, 13, 30), (DateTime?)Utc(11, 20, 13, 30)), (message.EventId, message.OldScheduledAt, message.NewScheduledAt));
    }

    /// <summary>
    /// BR-040：本次出现过的发布标识下，未来的"待公布"事件不在条目中的改为"待确认"；没有出现的发布标识不检查；
    /// 事件重新出现在日程中时回到"待公布"
    /// </summary>
    [Fact]
    public async Task Ingest_MarksDisappearedEventsUnconfirmedAndRestoresThem()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, _, source) = await CreateAsync(db);
        var (nfp, cpi) = (await AddIndicatorAsync(db), await AddIndicatorAsync(db));
        await MapAsync(db, source.Id, "empsit", nfp);
        await MapAsync(db, source.Id, "cpi", cpi);
        await ingestor.IngestAsync(Request(source.Id, [Entry("empsit", "2026-10", 11, 6), Entry("empsit", "2026-11", 12, 4), Entry("cpi", "2026-10", 11, 13)]), Ct);

        var dropped = await ingestor.IngestAsync(Request(source.Id, [Entry("empsit", "2026-10", 11, 6)]), Ct);
        var afterDrop = await StatusesAsync(db, nfp, cpi);
        var restored = await ingestor.IngestAsync(Request(source.Id, [Entry("empsit", "2026-10", 11, 6), Entry("empsit", "2026-11", 12, 4)]), Ct);

        Assert.Equal((1, 1), (dropped.Updated, restored.Updated));
        Assert.Equal(
            [(nfp, "2026-10", CalendarEventStatus.Scheduled), (nfp, "2026-11", CalendarEventStatus.Unconfirmed), (cpi, "2026-10", CalendarEventStatus.Scheduled)],
            afterDrop);
        Assert.Equal(CalendarEventStatus.Scheduled, (await StatusesAsync(db, nfp)).Single(s => s.Period == "2026-11").Status);
    }

    /// <summary>
    /// BR-156、ERR-EVT-003：没有映射的条目写入未匹配条目，已忽略的发布标识不写；日期已过的未匹配条目被清理。
    /// 加上映射后再同步，建出事件，未匹配条目被删除
    /// </summary>
    [Fact]
    public async Task Ingest_RecordsUnmatchedEntriesUntilMapped()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, _, source) = await CreateAsync(db);
        var productivity = await AddIndicatorAsync(db);
        db.IgnoredReleases.Add(new IgnoredRelease { SourceId = source.Id, ReleaseKey = "realer", ReleaseName = "Real Earnings" });
        db.UnmatchedEntries.Add(new UnmatchedEntry
        {
            SourceId = source.Id, ReleaseKey = "old", ReleaseName = "Old Release", ReferencePeriod = "2026-08", LocalDate = new DateOnly(2026, 9, 1),
            TimePrecision = TimePrecision.DateOnly, FirstSeenAt = Now, LastSeenAt = Now,
        });
        await db.SaveChangesAsync(Ct);
        IReadOnlyList<object> entries = [Entry("prod", "2026-09", 11, 5), Entry("realer", "2026-10", 11, 13)];

        var unmatched = await ingestor.IngestAsync(Request(source.Id, entries), Ct);
        var keysBeforeMapping = await UnmatchedKeysAsync(db, source.Id);
        await MapAsync(db, source.Id, "prod", productivity);
        var mapped = await ingestor.IngestAsync(Request(source.Id, entries), Ct);

        Assert.Equal((0, 2), (unmatched.Inserted, unmatched.Skipped));
        Assert.Equal(["prod"], keysBeforeMapping);
        Assert.Equal(1, mapped.Inserted);
        Assert.Empty(await UnmatchedKeysAsync(db, source.Id));
        Assert.Single(await EventsAsync(db, productivity));
    }

    /// <summary>
    /// 日历设计 §4.3：事件的排期由别的数据源维护时不改；没有维护排期的数据源的事件（如补建的）由本数据源接管。
    /// 参考期写法不对、精确到分钟却没有时间的条目计为失败（ERR-PARSE-005），已经过去的条目跳过
    /// </summary>
    [Fact]
    public async Task Ingest_RespectsOwnershipAndRejectsInvalidEntries()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, _, source) = await CreateAsync(db);
        var other = await TestData.AddDataSourceAsync(db, Ct);
        var nfp = await AddIndicatorAsync(db);
        await MapAsync(db, source.Id, "empsit", nfp);
        var owned = TestData.Event(nfp, "2026-10");
        owned.ScheduleSourceId = other.Id;
        var orphan = TestData.Event(nfp, "2026-11");
        orphan.Origin = EventOrigin.Supplemented;
        db.CalendarEvents.AddRange(owned, orphan);
        await db.SaveChangesAsync(Ct);
        IReadOnlyList<object> entries =
        [
            Entry("empsit", "2026-10", 11, 13),
            Entry("empsit", "2026-11", 12, 4),
            Entry("empsit", "2026-9", 12, 11),
            Entry("empsit", "2026-12", 12, 18) with { LocalTime = null },
            Entry("empsit", "2026-08", 10, 2),
        ];

        var result = await ingestor.IngestAsync(Request(source.Id, entries), Ct);

        Assert.Equal(new IngestResult(0, 1, 2, 2), result);
        var events = await EventsAsync(db, nfp);
        var ownedNow = events.Single(e => e.Id == owned.Id);
        var orphanNow = events.Single(e => e.Id == orphan.Id);
        Assert.Equal(((long?)other.Id, (DateTime?)Utc(11, 6, 13, 30)), (ownedNow.ScheduleSourceId, ownedNow.ScheduledAt));
        Assert.Equal(((long?)source.Id, "empsit", (DateTime?)Utc(12, 4, 13, 30)), (orphanNow.ScheduleSourceId, orphanNow.ScheduleReleaseKey, orphanNow.ScheduledAt));
    }

    private static async Task<(CalendarScheduleIngestor Ingestor, RecordingEventBus Bus, DataSource Source)> CreateAsync(SuanpanDbContext db)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(Now));
        var actor = new FixedAuditActorProvider("collector");
        var bus = new RecordingEventBus();
        var applier = new ScheduleApplier(db, actor, time, NullLogger<ScheduleApplier>.Instance);
        var ingestor = new CalendarScheduleIngestor(db, applier, actor, bus, time, NullLogger<CalendarScheduleIngestor>.Instance);
        return (ingestor, bus, await TestData.AddDataSourceAsync(db, Ct));
    }

    private static async Task<string> AddIndicatorAsync(SuanpanDbContext db)
    {
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code), Ct);
        return code;
    }

    private static async Task MapAsync(SuanpanDbContext db, long sourceId, string releaseKey, params string[] codes)
    {
        db.IndicatorSources.AddRange(codes.Select(code => new IndicatorSource
        {
            IndicatorCode = code, SourceId = sourceId, Purpose = IndicatorSourcePurpose.Schedule, ExternalId = releaseKey, ParserKey = "us.test.schedule",
        }));
        await db.SaveChangesAsync(Ct);
    }

    /// <summary>纽约当地 08:30 发布的一期</summary>
    private static ScheduleEntryRecord Entry(string releaseKey, string period, int month, int day)
        => new(releaseKey, $"Release {releaseKey}", period, new DateOnly(2026, month, day), new TimeOnly(8, 30), TimePrecision.Minute, 0);

    private static IngestRequest Request(long sourceId, IReadOnlyList<object> records) => new(1, 1, sourceId, records, []);

    private static Task<List<CalendarEvent>> EventsAsync(SuanpanDbContext db, params string[] codes)
        => db.CalendarEvents.AsNoTracking().Where(e => codes.Contains(e.IndicatorCode)).OrderBy(e => e.IndicatorCode).ThenBy(e => e.ReferencePeriod).ToListAsync(Ct);

    private static async Task<List<(string Code, string Period, CalendarEventStatus Status)>> StatusesAsync(SuanpanDbContext db, params string[] codes)
        => (await EventsAsync(db, codes))
            .OrderBy(e => Array.IndexOf(codes, e.IndicatorCode)).ThenBy(e => e.ReferencePeriod)
            .Select(e => (e.IndicatorCode, e.ReferencePeriod, e.Status))
            .ToList();

    private static Task<List<string>> UnmatchedKeysAsync(SuanpanDbContext db, long sourceId)
        => db.UnmatchedEntries.AsNoTracking().Where(u => u.SourceId == sourceId).OrderBy(u => u.ReleaseKey).Select(u => u.ReleaseKey).ToListAsync(Ct);

    private static DateTime Utc(int month, int day, int hour, int minute) => new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
