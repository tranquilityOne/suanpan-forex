using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Watch;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Watch;
using Suanpan.Services.Collect.Watch.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Configs.Models;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Mvc;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>
/// 盯盘计划（采集设计 §5.7，FR-003 主流程第 1 步）。时间参数的合并在 Core 的 WatchRuleTests 中测试，这里测服务按库中的事件、映射、
/// 数据源和任务分路，以及不能盯盘的原因。TestData.Event 的排期为 2026-11-06 13:30 UTC；断言只看本测试建的数据源和事件
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class WatchPlanServiceTests(PostgresFixture postgres)
{
    private static readonly DateTime T = new(2026, 11, 6, 13, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// BR-014：同一数据源、同一排期时间的高重要性事件合并为一路，提前取映射中最大的；仅日期的不盯（BR-018）；
    /// 中重要性只在配置打开时纳入，低重要性不盯
    /// </summary>
    [Fact]
    public async Task Plan_MergesOneReportIntoOneGroup()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (source, task) = await AddSourceWithTaskAsync(db);
        var report = new List<CalendarEvent>();
        for (var i = 0; i < 3; i++)
        {
            report.Add(await AddWatchedEventAsync(db, source, task, Importance.High, leadSeconds: i == 1 ? (short)120 : null));
        }

        var later = await AddWatchedEventAsync(db, source, task, Importance.High, at: T.AddHours(1));
        var dateOnly = await AddWatchedEventAsync(db, source, task, Importance.High, precision: TimePrecision.DateOnly);
        var medium = await AddWatchedEventAsync(db, source, task, Importance.Medium);
        await AddWatchedEventAsync(db, source, task, Importance.Low);

        var plan = await Create(db).GetPlanAsync(T.AddHours(-1), T.AddHours(2), Ct);
        var withMedium = await Create(db, includeMedium: true).GetPlanAsync(T.AddHours(-1), T.AddHours(2), Ct);

        var groups = plan.Groups.Where(g => g.SourceId == source.Id).ToList();
        Assert.Equal([(T, 3), (T.AddHours(1), 1)], groups.Select(g => (g.ScheduledAt, g.Events.Count)));
        Assert.Equal(report.Select(e => e.Id).Order(), groups[0].Events.Select(e => e.EventId).Order());
        Assert.Equal((task.Id, TimeSpan.FromSeconds(120), T.AddSeconds(-120)), (groups[0].TaskId, groups[0].Timing.Lead, groups[0].Window.Start));
        Assert.Equal(later.Id, Assert.Single(groups[1].Events).EventId);
        Assert.DoesNotContain(plan.Groups.SelectMany(g => g.Events), e => e.EventId == dateOnly.Id);
        Assert.Contains(withMedium.Groups.Single(g => g.SourceId == source.Id && g.ScheduledAt == T).Events, e => e.EventId == medium.Id);
        Assert.Equal(4, withMedium.Groups.Single(g => g.SourceId == source.Id && g.ScheduledAt == T).Events.Count);
    }

    /// <summary>
    /// 主源停用时用启用的备源；没有映射、映射的数据源都停用、数据源没有启用的公布值任务的事件列为不能盯盘，
    /// 各告警一次（24 小时内不重复）；盯盘中的事件（服务重启后）也在计划中
    /// </summary>
    [Fact]
    public async Task Plan_UsesEnabledSourceAndListsProblems()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (disabledSource, _) = await AddSourceWithTaskAsync(db, enabled: false);
        var (backup, backupTask) = await AddSourceWithTaskAsync(db);
        var (noTaskSource, _) = await AddSourceWithTaskAsync(db, taskEnabled: false);

        var fallsBack = await AddEventAsync(db, Importance.High, CalendarEventStatus.Watching);
        Map(db, fallsBack.IndicatorCode, disabledSource, "us.test.schedule", priority: 1);
        Map(db, fallsBack.IndicatorCode, backup, backupTask.ParserKey, priority: 2);
        var noMapping = await AddEventAsync(db, Importance.High);
        var allDisabled = await AddEventAsync(db, Importance.High);
        Map(db, allDisabled.IndicatorCode, disabledSource, "us.test.schedule", priority: 1);
        var noTask = await AddEventAsync(db, Importance.High);
        Map(db, noTask.IndicatorCode, noTaskSource, "us.test.schedule", priority: 1);
        await db.SaveChangesAsync(Ct);
        var alerts = new RecordingAlertService();
        var cache = new FakeRedisCache();

        var plan = await Create(db).GetPlanAsync(T.AddHours(-1), T.AddHours(1), Ct);
        long[] ours = [noMapping.Id, allDisabled.Id, noTask.Id];
        var problems = plan.Problems.Where(p => ours.Contains(p.Event.EventId)).ToList();
        await Create(db, alerts: alerts, cache: cache).AlertUnreadyAsync(problems, Ct);
        await Create(db, alerts: alerts, cache: cache).AlertUnreadyAsync(problems, Ct);

        var group = Assert.Single(plan.Groups, g => g.Events.Any(e => e.EventId == fallsBack.Id));
        Assert.Equal((backup.Id, backupTask.Id, CalendarEventStatus.Watching), (group.SourceId, group.TaskId, group.Events[0].Status));
        Assert.Equal(
            [(noMapping.Id, WatchProblemKind.NoMapping), (allDisabled.Id, WatchProblemKind.SourceDisabled), (noTask.Id, WatchProblemKind.NoTask)],
            problems.Select(p => (p.Event.EventId, p.Kind)).OrderBy(p => p.EventId));
        Assert.Equal(3, alerts.Raised.Count);
    }

    /// <summary>
    /// 最近结束的盯盘：按盯盘开始时写的状态历史找出这一路的事件，入库时延 = 检测时间 − 排期时间（BR-019），没拿到值的为空
    /// </summary>
    [Fact]
    public async Task Recent_ListsEventsWithLatency()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (source, task) = await AddSourceWithTaskAsync(db);
        var released = await AddEventAsync(db, Importance.High);
        var missing = await AddEventAsync(db, Importance.High);
        var run = new CollectRun
        {
            TaskId = task.Id, SourceId = source.Id, Trigger = CollectRunTrigger.Watch, ScheduledFor = T, Status = CollectRunStatus.PartiallySucceeded,
            QueuedAt = T.AddMinutes(-1), StartedAt = T.AddMinutes(-1), EndedAt = T.AddMinutes(15),
        };
        db.CollectRuns.Add(run);
        await db.SaveChangesAsync(Ct);
        var calendar = new CalendarWatchService(db, new FixedAuditActorProvider("collector"), new RecordingAlertService(),
            new FixedTimeProvider(new DateTimeOffset(T.AddMinutes(-1))), NullLogger<CalendarWatchService>.Instance);
        await calendar.StartAsync([released.Id, missing.Id], source.Id, run.Id, Ct);
        await db.CalendarEvents.Where(e => e.Id == released.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.Status, CalendarEventStatus.Released).SetProperty(e => e.DetectedAt, T.AddSeconds(20)), Ct);

        var recent = Assert.Single(await Create(db).GetRecentAsync(T, Ct), r => r.RunId == run.Id);

        Assert.Equal((source.Id, T, CollectRunStatus.PartiallySucceeded), (recent.SourceId, recent.ScheduledAt, recent.Status));
        Assert.Equal(
            [(released.Id, (TimeSpan?)TimeSpan.FromSeconds(20)), (missing.Id, (TimeSpan?)null)],
            recent.Events.Select(e => (e.EventId, e.Latency)).OrderBy(e => e.EventId));
    }

    /// <summary>正在盯盘：只列运行中的盯盘记录，带最近一次请求和按最近两次请求推算的间隔（被限流时变长）</summary>
    [Fact]
    public async Task Active_ListsRunningWatchesWithLastAttempt()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (source, task) = await AddSourceWithTaskAsync(db);
        var watched = await AddEventAsync(db, Importance.High);
        var running = WatchRun(task, CollectRunStatus.Running, T.AddDays(-30));
        running.Attempts =
        [
            new CollectAttempt { No = 1, StartedAt = T.AddDays(-30).AddSeconds(-60), HttpStatus = 200, Changed = true },
            new CollectAttempt { No = 2, StartedAt = T.AddDays(-30).AddSeconds(-58), HttpStatus = 429, Error = "Too Many Requests" },
            new CollectAttempt { No = 3, StartedAt = T.AddDays(-30).AddSeconds(-54), HttpStatus = 200, Changed = false },
        ];
        running.AttemptCount = 3;
        var ended = WatchRun(task, CollectRunStatus.Succeeded, T.AddDays(-30).AddMinutes(5));
        db.CollectRuns.AddRange(running, ended);
        await db.SaveChangesAsync(Ct);
        var calendar = new CalendarWatchService(db, new FixedAuditActorProvider("collector"), new RecordingAlertService(),
            new FixedTimeProvider(new DateTimeOffset(T.AddDays(-30).AddMinutes(-1))), NullLogger<CalendarWatchService>.Instance);
        await calendar.StartAsync([watched.Id], source.Id, running.Id, Ct);

        var active = (await Create(db).GetActiveAsync(Ct)).Where(r => r.SourceId == source.Id).ToList();

        var item = Assert.Single(active);
        Assert.Equal((running.Id, (short)3, (TimeSpan?)TimeSpan.FromSeconds(4)), (item.RunId, item.LastAttempt!.No, item.Interval));
        Assert.Equal(watched.Id, Assert.Single(item.Events).EventId);
    }

    private static CollectRun WatchRun(CollectTask task, CollectRunStatus status, DateTime scheduledFor) => new()
    {
        TaskId = task.Id, SourceId = task.SourceId, Trigger = CollectRunTrigger.Watch, ScheduledFor = scheduledFor, Status = status,
        QueuedAt = scheduledFor.AddMinutes(-1), StartedAt = scheduledFor.AddMinutes(-1),
        EndedAt = status == CollectRunStatus.Running ? null : scheduledFor.AddMinutes(1),
    };

    private static WatchPlanService Create(
        SuanpanDbContext db, bool includeMedium = false, RecordingAlertService? alerts = null, FakeRedisCache? cache = null)
        => new(db, new StubConfigService(includeMedium), cache ?? new FakeRedisCache(), alerts ?? new RecordingAlertService(),
            NullLogger<WatchPlanService>.Instance);

    /// <summary>数据源（频率上限 2 次/秒）和它下面的公布值任务</summary>
    private static async Task<(DataSource Source, CollectTask Task)> AddSourceWithTaskAsync(
        SuanpanDbContext db, bool enabled = true, bool taskEnabled = true)
    {
        var source = await TestData.AddDataSourceAsync(db, Ct, enabled);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id, CollectTargetType.Release, taskEnabled);
        return (source, task);
    }

    /// <summary>新指标和它在 T（或 at）的事件，指标映射到给定的数据源，解析器与任务相同</summary>
    private static async Task<CalendarEvent> AddWatchedEventAsync(
        SuanpanDbContext db, DataSource source, CollectTask task, Importance importance, short? leadSeconds = null, DateTime? at = null,
        TimePrecision precision = TimePrecision.Minute)
    {
        var calendarEvent = await AddEventAsync(db, importance, at: at, precision: precision);
        Map(db, calendarEvent.IndicatorCode, source, task.ParserKey, priority: 1, leadSeconds);
        await db.SaveChangesAsync(Ct);
        return calendarEvent;
    }

    private static async Task<CalendarEvent> AddEventAsync(
        SuanpanDbContext db, Importance importance, CalendarEventStatus status = CalendarEventStatus.Scheduled, DateTime? at = null,
        TimePrecision precision = TimePrecision.Minute)
    {
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code) with { Importance = importance }, Ct);
        var calendarEvent = TestData.Event(code);
        calendarEvent.Status = status;
        calendarEvent.ScheduledAt = at ?? T;
        calendarEvent.TimePrecision = precision;
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(Ct);
        return calendarEvent;
    }

    private static void Map(SuanpanDbContext db, string code, DataSource source, string parserKey, short priority, short? leadSeconds = null)
        => db.IndicatorSources.Add(new IndicatorSource
        {
            IndicatorCode = code, SourceId = source.Id, Purpose = IndicatorSourcePurpose.Release, ExternalId = code, ParserKey = parserKey,
            Priority = priority, WatchLeadSeconds = leadSeconds,
        });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>只给出是否包括中重要性，其余配置取默认值；配置服务本身在 Sys 的测试中测试</summary>
    private sealed class StubConfigService(bool includeMedium) : IConfigService
    {
        public Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default)
            => Task.FromResult(key == CollectConfigKeys.WatchIncludeMedium ? includeMedium : defaultValue);

        public Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<string?> GetStringAsync(string key, string? defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<IReadOnlyList<ConfigRow>> GetListAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ManageResult> SaveAsync(string key, string value, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ManageResult> ResetAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
