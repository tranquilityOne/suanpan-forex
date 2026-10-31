using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Collector.Tests.Fixtures;
using Suanpan.Collector.Watch;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Ops.Enums;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Collect.Watch.Models;
using Xunit;

namespace Suanpan.Collector.Tests.Watch;

/// <summary>
/// 一路盯盘（采集设计 §5.7，FR-003 验收第 1 ~ 4、6 条，ERR-COL-005、ERR-COL-006）：用 BLS 真实快照回放，手动推进的时钟控制时刻，
/// 公布值入库、日历状态都是真实的。各测试的排期时间不同，盯盘计划互不影响
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class WatchSessionTests(CollectorFixture fixture)
{
    private static readonly DateTime BaseTime = new(2031, 1, 10, 13, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// FR-003 验收第 2、3、6 条：三个指标出自同一份报告，每次只发一路请求；内容没变化时不解析；官方在 T 发布后，
    /// 下一次请求就拿到全部三个值（时延不超过一个请求间隔），之后不再请求，运行记录为成功
    /// </summary>
    [Fact]
    public async Task Replay_OneRequestPerPollAndStopsOnceReleased()
    {
        var (app, scenario, group) = await SetUpAsync(BaseTime);
        Assert.Equal(3, group.Events.Count);
        var clock = new ManualTimeProvider(scenario.ScheduledAt.AddSeconds(-60));
        var session = Session(app, group, clock);

        Assert.True(await session.BeginAsync(Ct));
        Assert.All(await scenario.EventsAsync(app), e => Assert.Equal(CalendarEventStatus.Watching, e.Status));
        await session.PollAsync(Ct);
        clock.Advance(TimeSpan.FromSeconds(2));
        await session.PollAsync(Ct);
        clock.Advance(TimeSpan.FromSeconds(58));
        scenario.Release();
        await session.PollAsync(Ct);
        await session.EndAsync(null, Ct);

        Assert.Empty(session.Pending);
        Assert.Equal(3, fixture.Http.Requests(scenario.Host).Count);
        var run = await app.GetRunAsync(session.RunId);
        Assert.Equal((CollectRunStatus.Succeeded, CollectRunTrigger.Watch, (DateTime?)scenario.ScheduledAt), (run.Status, run.Trigger, run.ScheduledFor));
        Assert.Equal([true, false, true], run.Attempts.Select(a => a.Changed ?? false));
        Assert.True(run.Attempts[2].StartedAt - scenario.ScheduledAt <= group.Timing.Interval);
        Assert.All(await scenario.EventsAsync(app), e => Assert.Equal((CalendarEventStatus.Released, true), (e.Status, e.DetectedAt is not null)));
    }

    /// <summary>FR-003 验收第 4 条：官方延迟，T+10 分钟事件改为延迟并告警（高重要性为紧急）；窗口结束后运行记录为失败（官方延迟）</summary>
    [Fact]
    public async Task NoValue_DelaysAtTenMinutes()
    {
        var (app, scenario, group) = await SetUpAsync(BaseTime.AddDays(1));
        var clock = new ManualTimeProvider(scenario.ScheduledAt.AddSeconds(-60));
        var session = Session(app, group, clock);

        await session.BeginAsync(Ct);
        await session.PollAsync(Ct);
        clock.Advance(TimeSpan.FromSeconds(60) + WatchRule.DelayAfter);
        await session.PollAsync(Ct);
        clock.Advance(group.Window.End - group.Window.DelayAt);
        await session.EndAsync(null, Ct);

        Assert.All(await scenario.EventsAsync(app), e => Assert.Equal(CalendarEventStatus.Delayed, e.Status));
        var codes = scenario.Events.Select(e => e.IndicatorCode).ToHashSet();
        Assert.Equal(3, app.Alerts.Raised.Count(a => codes.Contains(a.Subject) && a.Level == AlertLevel.Urgent));
        var run = await app.GetRunAsync(session.RunId);
        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.OfficialDelay), (run.Status, run.ErrorCode));
    }

    /// <summary>ERR-COL-005：429 时间隔加倍并告警一次（对象为数据源），再次 429 继续加倍；成功后恢复原间隔</summary>
    [Fact]
    public async Task Throttled_DoublesIntervalUntilSuccess()
    {
        var (app, scenario, group) = await SetUpAsync(BaseTime.AddDays(2));
        var session = Session(app, group, new ManualTimeProvider(scenario.ScheduledAt.AddSeconds(-60)));
        await session.BeginAsync(Ct);

        scenario.Status = HttpStatusCode.TooManyRequests;
        await session.PollAsync(Ct);
        var first = session.Interval;
        await session.PollAsync(Ct);
        var second = session.Interval;
        scenario.Status = null;
        await session.PollAsync(Ct);

        Assert.Equal((group.Timing.Interval * 2, group.Timing.Interval * 4, group.Timing.Interval), (first, second, session.Interval));
        Assert.Single(app.Alerts.Raised, a => a.Subject == scenario.Source.Name);
    }

    /// <summary>
    /// ERR-COL-006：服务重启后，新的一路接着写原来的运行记录（同一任务同一排期时间只有一条）；
    /// 盯盘中的事件被官方改期后退出本路并改回待公布（PRD v1.0.0.26）
    /// </summary>
    [Fact]
    public async Task Restart_ResumesTheRunAndReleasesRescheduledEvents()
    {
        var (app, scenario, group) = await SetUpAsync(BaseTime.AddDays(3));
        var clock = new ManualTimeProvider(scenario.ScheduledAt.AddSeconds(-60));
        var before = Session(app, group, clock);
        await before.BeginAsync(Ct);
        await before.PollAsync(Ct);

        var rescheduled = scenario.Events[0].Id;
        var later = scenario.ScheduledAt.AddDays(1);
        await app.ExecuteAsync(db => db.CalendarEvents.Where(e => e.Id == rescheduled).ExecuteUpdateAsync(u => u.SetProperty(e => e.ScheduledAt, later)));
        var after = Session(app, group, clock);
        Assert.True(await after.BeginAsync(Ct));
        clock.Advance(TimeSpan.FromSeconds(2));
        await after.PollAsync(Ct);

        Assert.Equal(before.RunId, after.RunId);
        Assert.Equal(2, (await app.GetRunAsync(after.RunId)).AttemptCount);
        Assert.DoesNotContain(rescheduled, after.Pending);
        Assert.Equal(CalendarEventStatus.Scheduled, (await scenario.EventsAsync(app)).Single(e => e.Id == rescheduled).Status);
    }

    /// <summary>FR-003 验收第 1 条：按时间运行时，在排期前 60 秒发出第一次请求，之前不请求</summary>
    [Fact]
    public async Task Run_StartsSixtySecondsBeforeRelease()
    {
        var (app, scenario, group) = await SetUpAsync(BaseTime.AddDays(4));
        var clock = new ManualTimeProvider(scenario.ScheduledAt.AddSeconds(-61));
        var session = Session(app, group, clock);
        using var stop = new CancellationTokenSource();

        var running = Task.Run(() => session.RunAsync(stop.Token), Ct);
        clock.Advance(TimeSpan.FromSeconds(1));
        var run = await app.WaitForRunAsync(await RunIdAsync(app, session), r => r.Attempts.Count >= 1);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

        Assert.Equal(scenario.ScheduledAt.AddSeconds(-60), run.Attempts[0].StartedAt);
    }

    /// <summary>
    /// 窗口在服务中断期间结束了：盯盘服务计划时为这一路建运行记录、把没拿到值的事件改为延迟，运行记录记为中断（ERR-COL-009）。
    /// 排期时间最早，计划时不会碰到其他测试的盯盘
    /// </summary>
    [Fact]
    public async Task Tick_FinishesAMissedWindow()
    {
        var (app, scenario, group) = await SetUpAsync(new DateTime(2030, 1, 10, 13, 30, 0, DateTimeKind.Utc));
        var clock = new ManualTimeProvider(group.Window.End.AddMinutes(5));
        var service = new WatchService(app.Services.GetRequiredService<IServiceScopeFactory>(), app.Services.GetRequiredService<IDistributedLock>(),
            clock, NullLogger<WatchService>.Instance);

        await service.TickAsync(Ct);

        Assert.All(await scenario.EventsAsync(app), e => Assert.Equal(CalendarEventStatus.Delayed, e.Status));
        CollectRun run = null!;
        await app.ExecuteAsync(async db => run = await db.CollectRuns.AsNoTracking()
            .SingleAsync(r => r.TaskId == scenario.Task.Id && r.Trigger == CollectRunTrigger.Watch));
        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.Interrupted), (run.Status, run.ErrorCode));
        Assert.Empty(service.Sessions);
    }

    private async Task<(CollectorApp App, BlsWatchScenario Scenario, WatchGroup Group)> SetUpAsync(DateTime at)
    {
        var app = await fixture.GetAppAsync();
        var scenario = await BlsWatchScenario.CreateAsync(fixture, app, at);
        return (app, scenario, await scenario.GroupAsync(app));
    }

    private static WatchSession Session(CollectorApp app, WatchGroup group, TimeProvider clock)
        => new(group, app.Services.GetRequiredService<IServiceScopeFactory>(), clock, NullLogger.Instance);

    /// <summary>按时间运行时运行记录在开始时间才建，等它出现</summary>
    private static async Task<long> RunIdAsync(CollectorApp app, WatchSession session)
        => (await Eventually.GetAsync(() => Task.FromResult(session.RunId), id => id > 0, _ => "这一路还没有建运行记录"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
