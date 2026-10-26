using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Data;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Runs.Models;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Messages;
using Suanpan.Shared.Messages.Collect;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>
/// 运行记录的创建与下发、查询（采集设计 §4.1、§4.4、§8.1，FR-002）。消息总线用替身，记下发布的消息；
/// 编辑能触发哪些目标数据类型的分支在 CollectTaskRuleTests 中测试
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CollectRunServiceTests(PostgresFixture postgres)
{
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

    private readonly RecordingEventBus _bus = new();

    /// <summary>
    /// FR-002 主流程第 5 步、BR-155：停用的任务也能立即执行；创建"排队"的运行记录，记录触发人，提交后下发第 1 次请求的指令
    /// </summary>
    [Fact]
    public async Task Trigger_QueuesManualRunAndPublishesCommand()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id, enabled: false);

        var result = await Create(db).TriggerAsync(task.Id, "editor1", anyTargetType: false, Ct);

        Assert.Equal((true, true), (result.Success, result.Published));
        var run = await db.CollectRuns.AsNoTracking().SingleAsync(r => r.TaskId == task.Id, Ct);
        Assert.Equal(
            (result.RunId, source.Id, CollectRunTrigger.Manual, CollectRunStatus.Queued, "editor1", _clock.UtcNow.UtcDateTime),
            ((long?)run.Id, run.SourceId, run.Trigger, run.Status, run.TriggeredBy, run.QueuedAt));
        var command = Assert.Single(_bus.Published);
        Assert.Equal((run.Id, 1), (command.RunId, command.Attempt));
    }

    /// <summary>BR-155：数据源停用时不能执行；PRD §13.1.2：编辑不能触发日历排期、公布值以外的任务。都不创建运行记录</summary>
    [Fact]
    public async Task Trigger_RejectsDisabledSourceAndOtherTargetTypesForEditor()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var disabledSource = await TestData.AddDataSourceAsync(db, Ct, enabled: false);
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var blocked = await TestData.AddCollectTaskAsync(db, Ct, disabledSource.Id);
        var news = await TestData.AddCollectTaskAsync(db, Ct, source.Id, CollectTargetType.News);

        var sourceDisabled = await service.TriggerAsync(blocked.Id, "engineer1", anyTargetType: true, Ct);
        var editorOnNews = await service.TriggerAsync(news.Id, "editor1", anyTargetType: false, Ct);
        var engineerOnNews = await service.TriggerAsync(news.Id, "engineer1", anyTargetType: true, Ct);

        Assert.Equal((false, false, true), (sourceDisabled.Success, editorOnNews.Success, engineerOnNews.Success));
        Assert.Equal([news.Id], await db.CollectRuns.Where(r => r.TaskId == blocked.Id || r.TaskId == news.Id).Select(r => r.TaskId).ToListAsync(Ct));
    }

    /// <summary>BR-155：停用的任务不参与定时调度</summary>
    [Fact]
    public async Task Enqueue_ScheduledRequiresEnabledTask()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id, enabled: false, cron: "0 1 * * *");

        var result = await Create(db).EnqueueAsync(task.Id, CollectRunTrigger.Scheduled, _clock.UtcNow.UtcDateTime, triggeredBy: null, Ct);

        Assert.False(result.Success);
        Assert.Empty(_bus.Published);
    }

    /// <summary>采集设计 §4.2：提交后下发失败时不报错，运行记录停在"排队"，等调度分发重新发布</summary>
    [Fact]
    public async Task Enqueue_KeepsQueuedRunWhenPublishFails()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id);
        _bus.Fail = true;

        var result = await Create(db).TriggerAsync(task.Id, "engineer1", anyTargetType: true, Ct);

        Assert.Equal((true, false), (result.Success, result.Published));
        Assert.Equal(CollectRunStatus.Queued, (await db.CollectRuns.AsNoTracking().SingleAsync(r => r.Id == result.RunId, Ct)).Status);
    }

    /// <summary>FR-002 验收第 5 条：按任务、数据源、状态、触发时间筛选，按触发时间倒序</summary>
    [Fact]
    public async Task Query_FiltersByTaskSourceStatusAndTime()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id);
        var other = await TestData.AddCollectTaskAsync(db, Ct, source.Id);
        var now = _clock.UtcNow.UtcDateTime;
        var failedEarly = Run(task, CollectRunStatus.Failed, now.AddDays(-2));
        var failedLate = Run(task, CollectRunStatus.Failed, now.AddHours(-1));
        var succeeded = Run(task, CollectRunStatus.Succeeded, now.AddHours(-2));
        var otherTask = Run(other, CollectRunStatus.Failed, now.AddHours(-3));
        db.CollectRuns.AddRange(failedEarly, failedLate, succeeded, otherTask);
        await db.SaveChangesAsync(Ct);
        var service = Create(db);

        var bySource = await service.QueryAsync(new CollectRunQuery(SourceId: source.Id), Ct);
        var byTaskAndStatus = await service.QueryAsync(new CollectRunQuery(TaskId: task.Id, Status: CollectRunStatus.Failed), Ct);
        var byTime = await service.QueryAsync(new CollectRunQuery(SourceId: source.Id, Begin: now.AddDays(-1), End: now.AddHours(-1)), Ct);

        Assert.Equal([failedLate.Id, succeeded.Id, otherTask.Id, failedEarly.Id], bySource.Items.Select(r => r.Id));
        Assert.Equal([failedLate.Id, failedEarly.Id], byTaskAndStatus.Items.Select(r => r.Id));
        Assert.Equal([succeeded.Id, otherTask.Id], byTime.Items.Select(r => r.Id));
    }

    /// <summary>运行详情带任务和数据源的名称，每次请求的记录从 jsonb 列读出（FR-002 验收第 3 条）</summary>
    [Fact]
    public async Task Get_ReturnsRunWithAttempts()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id);
        var run = Run(task, CollectRunStatus.Failed, _clock.UtcNow.UtcDateTime);
        run.Attempts =
        [
            new CollectAttempt { No = 1, StartedAt = run.QueuedAt, DurationMs = 812, HttpStatus = 503, Error = "Service Unavailable", DeferredMs = 1500 },
            new CollectAttempt { No = 2, StartedAt = run.QueuedAt.AddSeconds(30), DurationMs = 10000, Error = "请求超时" },
        ];
        db.CollectRuns.Add(run);
        await db.SaveChangesAsync(Ct);

        var detail = await Create(db).GetAsync(run.Id, Ct);

        Assert.NotNull(detail);
        Assert.Equal((task.Name, source.Name, task.TargetType), (detail.TaskName, detail.SourceName, detail.TargetType));
        Assert.Equal([(1, 503, 1500), (2, (int?)null, 0)], detail.Run.Attempts.Select(a => ((int)a.No, a.HttpStatus, a.DeferredMs)));
    }

    private CollectRunService Create(SuanpanDbContext db) => new(db, _bus, _clock, NullLogger<CollectRunService>.Instance);

    private static CollectRun Run(CollectTask task, CollectRunStatus status, DateTime queuedAt) => new()
    {
        TaskId = task.Id,
        SourceId = task.SourceId,
        Trigger = CollectRunTrigger.Scheduled,
        ScheduledFor = queuedAt,
        Status = status,
        QueuedAt = queuedAt,
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>记下发布的消息；Fail 为 true 时模拟 RabbitMQ 不可用</summary>
    private sealed class RecordingEventBus : IEventBus
    {
        public List<CollectRunCommand> Published { get; } = [];

        public bool Fail { get; set; }

        public Task PublishAsync<TMessage>(TMessage message, TimeSpan? delay = null, CancellationToken ct = default)
            where TMessage : IntegrationMessage
        {
            if (Fail)
            {
                throw new InvalidOperationException("RabbitMQ 不可用");
            }

            Published.Add((CollectRunCommand)(object)message);
            return Task.CompletedTask;
        }
    }
}
