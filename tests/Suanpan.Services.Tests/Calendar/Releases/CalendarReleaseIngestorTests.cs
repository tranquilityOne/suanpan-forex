using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Records;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Ops.Enums;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Releases;
using Suanpan.Services.Collect.Ingest;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Messages.Calendar;
using Xunit;

namespace Suanpan.Services.Tests.Calendar.Releases;

/// <summary>
/// 公布值入库（FR-008，日历设计 §5）。现在是 UTC 2026-10-29 12:00；测试指标为月度、1 位小数、单位百分比。
/// 规则的分支在 Core 的 ReleaseValueRuleTests 中测试，这里测入库接上了规则以及数据库中的结果。断言只看本测试建的指标和数据源
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CalendarReleaseIngestorTests(PostgresFixture postgres)
{
    private const string ExternalId = "TEST0001:pct";

    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// FR-008 验收第 1、3 条，BR-043 ~ BR-045：最新一期变为"已公布"（25.45 按 .5 远离零舍入为 25.5），记下来源快照和检测时间；
    /// 前值取上一期的公布值，数据中上一期的值不同就是前值修正，上一期的修正后数值同步更新；公布值填为下一期的前值；
    /// 快照被标记为被引用。同样的数据再入库一次没有任何改动
    /// </summary>
    [Fact]
    public async Task Ingest_ReleasesLatestAndRevisesThePreviousPeriod()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, bus, _, source) = await CreateAsync(db);
        var code = await AddIndicatorAsync(db, source.Id);
        var august = Event(code, "2026-08", CalendarEventStatus.Released, 22.0m);
        var september = Event(code, "2026-09", CalendarEventStatus.Scheduled, null);
        var october = Event(code, "2026-10", CalendarEventStatus.Scheduled, null);
        db.CalendarEvents.AddRange(august, september, october);
        var snapshot = Snapshot(source.Id);
        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync(Ct);
        IReadOnlyList<object> records = [Record("2026-08", -4.04m), Record("2026-09", 25.45m)];

        var first = await ingestor.IngestAsync(Request(source.Id, records, snapshot.Id), Ct);
        var changesAfterFirst = await db.CalendarEventChanges.CountAsync(c => c.EventId == august.Id || c.EventId == september.Id || c.EventId == october.Id, Ct);
        var second = await ingestor.IngestAsync(Request(source.Id, records, snapshot.Id), Ct);

        Assert.Equal(new IngestResult(0, 2, 0, 0), first);
        Assert.Equal(new IngestResult(0, 0, 2, 0), second);
        var events = (await EventsAsync(db, code)).ToDictionary(e => e.ReferencePeriod);
        var released = events["2026-09"];
        Assert.Equal(
            (CalendarEventStatus.Released, (decimal?)25.5m, (long?)source.Id, (DateTime?)Now, (long?)snapshot.Id, (decimal?)22.0m, (decimal?)-4.0m),
            (released.Status, released.Actual, released.ActualSourceId, released.DetectedAt, released.SourceSnapshotId, released.Previous, released.PreviousRevised));
        Assert.Equal(((decimal?)22.0m, (decimal?)-4.0m), (events["2026-08"].Actual, events["2026-08"].RevisedValue));
        Assert.Equal(25.5m, events["2026-10"].Previous);
        Assert.Equal(25.5m, (await db.CalendarEventValues.SingleAsync(v => v.EventId == september.Id, Ct)).Value);
        Assert.True((await db.Snapshots.SingleAsync(s => s.Id == snapshot.Id, Ct)).Referenced);
        Assert.Equal(september.Id, Assert.Single(bus.Published.OfType<EventReleasedEvent>()).EventId);
        Assert.Equal(changesAfterFirst, await db.CalendarEventChanges.CountAsync(c => c.EventId == august.Id || c.EventId == september.Id || c.EventId == october.Id, Ct));
    }

    /// <summary>
    /// FR-008 验收第 2 条、BR-046：已公布的值在仍是最新一期时再采集到不同数值，属于官方更正：原公布值不变，进入"待复核"，
    /// 发出 EventHeldForReviewEvent 并告警（ERR-EVT-007，非高重要性为"重要"）
    /// </summary>
    [Fact]
    public async Task Ingest_CorrectionOfTheLatestPeriodGoesToReview()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, bus, alerts, source) = await CreateAsync(db);
        var code = await AddIndicatorAsync(db, source.Id);
        var september = Event(code, "2026-09", CalendarEventStatus.Released, 25.5m);
        db.CalendarEvents.Add(september);
        await db.SaveChangesAsync(Ct);

        await ingestor.IngestAsync(Request(source.Id, [Record("2026-09", 26.0m)]), Ct);

        var reviewed = Assert.Single(await EventsAsync(db, code));
        Assert.Equal((CalendarEventStatus.PendingReview, (decimal?)25.5m), (reviewed.Status, reviewed.Actual));
        Assert.Equal(26.0m, (await db.CalendarEventValues.SingleAsync(v => v.EventId == september.Id, Ct)).Value);
        Assert.Equal(september.Id, Assert.Single(bus.Published.OfType<EventHeldForReviewEvent>()).EventId);
        Assert.Contains((AlertLevel.Important, code), alerts.Raised);
    }

    /// <summary>
    /// BR-157：刚开始采集（指标还没有公布过的事件）时不补建，最新一期的值填为下一个事件的前值；
    /// 有公布过的事件后，最新一期找不到事件就补建（ERR-EVT-006）：来源"补建"、排期时间为检测时间，并告警
    /// </summary>
    [Fact]
    public async Task Ingest_SupplementsOnlyAfterSomethingWasReleased()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, _, alerts, source) = await CreateAsync(db);
        var fresh = await AddIndicatorAsync(db, source.Id, "FRESH:pct");
        var running = await AddIndicatorAsync(db, source.Id, "RUNNING:pct");
        db.CalendarEvents.AddRange(Event(fresh, "2026-10", CalendarEventStatus.Scheduled, null), Event(running, "2026-08", CalendarEventStatus.Released, 22.0m));
        await db.SaveChangesAsync(Ct);

        var result = await ingestor.IngestAsync(Request(source.Id,
        [
            Record("2026-08", 21.0m, "FRESH:pct"), Record("2026-09", 25.0m, "FRESH:pct"),
            Record("2026-08", 22.0m, "RUNNING:pct"), Record("2026-09", 25.0m, "RUNNING:pct"),
        ]), Ct);

        Assert.Equal(new IngestResult(1, 0, 3, 0), result);
        var freshEvents = await EventsAsync(db, fresh);
        Assert.Equal([("2026-10", (decimal?)25.0m)], freshEvents.Select(e => (e.ReferencePeriod, e.Previous)));
        var supplemented = (await EventsAsync(db, running)).Single(e => e.ReferencePeriod == "2026-09");
        Assert.Equal(
            (EventOrigin.Supplemented, CalendarEventStatus.Released, (DateTime?)Now, TimePrecision.Minute, (decimal?)22.0m),
            (supplemented.Origin, supplemented.Status, supplemented.ScheduledAt, supplemented.TimePrecision, supplemented.Previous));
        Assert.Equal([(AlertLevel.Important, running)], alerts.Raised);
    }

    /// <summary>
    /// BR-062：公布值、前值被人工锁定时不覆盖，值不同时各写一条锁定冲突，公布值的冲突计入"因锁定跳过"；再次冲突只更新同一条
    /// </summary>
    [Fact]
    public async Task Ingest_LockedFieldsRecordOneConflictEach()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, _, _, source) = await CreateAsync(db);
        var code = await AddIndicatorAsync(db, source.Id);
        var september = Event(code, "2026-09", CalendarEventStatus.Released, 25.5m);
        september.Previous = 20.0m;
        EventLockRule.Lock(september.LockedFields, EventField.Actual, "editor", Now, "人工修正");
        EventLockRule.Lock(september.LockedFields, EventField.Previous, "editor", Now, "人工修正");
        db.CalendarEvents.AddRange(Event(code, "2026-08", CalendarEventStatus.Released, 22.0m), september);
        await db.SaveChangesAsync(Ct);

        var first = await ingestor.IngestAsync(Request(source.Id, [Record("2026-08", 22.0m), Record("2026-09", 26.0m)]), Ct);
        await ingestor.IngestAsync(Request(source.Id, [Record("2026-08", 22.0m), Record("2026-09", 27.0m)]), Ct);

        Assert.Equal(1, first.Locked);
        var locked = (await EventsAsync(db, code)).Single(e => e.Id == september.Id);
        Assert.Equal(((decimal?)25.5m, (decimal?)20.0m), (locked.Actual, locked.Previous));
        var conflicts = await db.LockConflicts.AsNoTracking().Where(c => c.EventId == september.Id).OrderBy(c => c.Field).ToListAsync(Ct);
        Assert.Equal(
            [(EventField.Actual, LockConflictStatus.Pending), (EventField.Previous, LockConflictStatus.Pending)],
            conflicts.Select(c => (c.Field, c.Status)));
        Assert.Contains("27", conflicts[0].CollectedValue);
    }

    /// <summary>ERR-EVT-002、BR-129：质量校验未通过时进入"待复核"，公布值不写入事件；高重要性事件的告警为"紧急"</summary>
    [Fact]
    public async Task Ingest_FailedValidationHoldsForReview()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, bus, alerts, source) = await CreateAsync(db, new FailingValidator());
        var code = await AddIndicatorAsync(db, source.Id, importance: Importance.High);
        db.CalendarEvents.Add(Event(code, "2026-09", CalendarEventStatus.Watching, null));
        await db.SaveChangesAsync(Ct);

        await ingestor.IngestAsync(Request(source.Id, [Record("2026-09", 99.0m)]), Ct);

        var held = Assert.Single(await EventsAsync(db, code));
        Assert.Equal((CalendarEventStatus.PendingReview, (decimal?)null), (held.Status, held.Actual));
        Assert.Single(bus.Published.OfType<EventHeldForReviewEvent>());
        Assert.Equal([(AlertLevel.Urgent, code)], alerts.Raised);
    }

    /// <summary>ERR-PARSE-005、ERR-PARSE-006：参考期写法不对、单位与指标不同的计为失败；没有映射的外部标识计为跳过</summary>
    [Fact]
    public async Task Ingest_RejectsInvalidRecords()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (ingestor, _, _, source) = await CreateAsync(db);
        var code = await AddIndicatorAsync(db, source.Id);

        var result = await ingestor.IngestAsync(Request(source.Id,
        [
            Record("2026-9", 1m),
            Record("2026-09", 1m) with { Unit = IndicatorUnit.ThousandPersons },
            Record("2026-09", 1m, "UNKNOWN"),
        ]), Ct);

        Assert.Equal(new IngestResult(0, 0, 1, 2), result);
        Assert.Empty(await EventsAsync(db, code));
    }

    private static async Task<(CalendarReleaseIngestor Ingestor, RecordingEventBus Bus, RecordingAlertService Alerts, DataSource Source)> CreateAsync(
        SuanpanDbContext db, IReleaseValidator? validator = null)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(Now));
        var bus = new RecordingEventBus();
        var alerts = new RecordingAlertService();
        var snapshots = new SnapshotService(db, new PostgresSnapshotContentStore(db, time));
        var ingestor = new CalendarReleaseIngestor(db, validator ?? new PassThroughReleaseValidator(), snapshots, alerts,
            new FixedAuditActorProvider("collector"), bus, time, NullLogger<CalendarReleaseIngestor>.Instance);
        return (ingestor, bus, alerts, await TestData.AddDataSourceAsync(db, Ct));
    }

    /// <summary>新指标及其公布值映射（主源）</summary>
    private static async Task<string> AddIndicatorAsync(SuanpanDbContext db, long sourceId, string externalId = ExternalId, Importance importance = Importance.Medium)
    {
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code) with { Importance = importance }, Ct);
        db.IndicatorSources.Add(new IndicatorSource
        {
            IndicatorCode = code, SourceId = sourceId, Purpose = IndicatorSourcePurpose.Release, ExternalId = externalId,
            ParserKey = "us.test.release", Priority = 1,
        });
        await db.SaveChangesAsync(Ct);
        return code;
    }

    private static CalendarEvent Event(string code, string period, CalendarEventStatus status, decimal? actual)
    {
        var calendarEvent = TestData.Event(code, period);
        calendarEvent.Status = status;
        calendarEvent.Actual = actual;
        return calendarEvent;
    }

    private static Snapshot Snapshot(long sourceId) => new()
    {
        RunId = 1, Attempt = 1, SourceId = sourceId, TaskId = 1, Url = "https://api.example.org/data", RequestKey = TestData.Unique("key"),
        FetchedAt = Now, HttpStatus = 200, ContentHash = new string('a', 64), Size = 10,
    };

    private static ReleaseValueRecord Record(string period, decimal value, string externalId = ExternalId)
        => new(externalId, period, value, null, IndicatorUnit.Percent, value.ToString(System.Globalization.CultureInfo.InvariantCulture), 0);

    private static IngestRequest Request(long sourceId, IReadOnlyList<object> records, long? snapshotId = null)
        => new(1, 1, sourceId, records, snapshotId is { } id ? [id] : []);

    private static Task<List<CalendarEvent>> EventsAsync(SuanpanDbContext db, string code)
        => db.CalendarEvents.AsNoTracking().Where(e => e.IndicatorCode == code).OrderBy(e => e.ReferencePeriod).ToListAsync(Ct);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FailingValidator : IReleaseValidator
    {
        public Task<ReleaseValidation> ValidateAsync(ReleaseCandidate candidate, CancellationToken ct)
            => Task.FromResult(ReleaseValidation.Fail("超出合理范围"));
    }
}
