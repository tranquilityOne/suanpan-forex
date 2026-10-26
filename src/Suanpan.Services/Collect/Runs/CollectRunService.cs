using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Data;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Services.Collect.Runs.Models;
using Suanpan.Services.Common.Querying;
using Suanpan.Shared.Messages.Collect;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Collect.Runs;

public sealed class CollectRunService(
    SuanpanDbContext db,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<CollectRunService> logger) : ICollectRunService
{
    public Task<PagedResult<CollectRunRow>> QueryAsync(CollectRunQuery query, CancellationToken ct = default)
    {
        var runs = db.CollectRuns.AsNoTracking();
        if (query.TaskId is { } taskId)
        {
            runs = runs.Where(r => r.TaskId == taskId);
        }

        if (query.SourceId is { } sourceId)
        {
            runs = runs.Where(r => r.SourceId == sourceId);
        }

        if (query.Status is { } status)
        {
            runs = runs.Where(r => r.Status == status);
        }

        if (query.Trigger is { } trigger)
        {
            runs = runs.Where(r => r.Trigger == trigger);
        }

        if (query.Begin is { } begin)
        {
            runs = runs.Where(r => r.QueuedAt >= begin);
        }

        if (query.End is { } end)
        {
            runs = runs.Where(r => r.QueuedAt < end);
        }

        // 有运行记录的任务不能删除（BR-154），所以总能关联到任务
        return (from r in runs
                join t in db.CollectTasks on r.TaskId equals t.Id
                orderby r.QueuedAt descending, r.Id descending
                select new CollectRunRow(
                    r.Id, r.TaskId, t.Name, r.Trigger, r.TriggeredBy, r.Status, r.QueuedAt, r.StartedAt, r.EndedAt,
                    r.AttemptCount, r.InsertedCount, r.UpdatedCount, r.SkippedCount))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
    }

    public Task<CollectRunDetail?> GetAsync(long id, CancellationToken ct = default)
        => (from r in db.CollectRuns.AsNoTracking()
            where r.Id == id
            join t in db.CollectTasks on r.TaskId equals t.Id
            join s in db.DataSources on r.SourceId equals s.Id into sources
            from s in sources.DefaultIfEmpty()
            select new CollectRunDetail(r, t.Name, t.TargetType, s == null ? null : s.Name))
            .FirstOrDefaultAsync(ct);

    public async Task<EnqueueResult> EnqueueAsync(
        long taskId, CollectRunTrigger trigger, DateTime? scheduledFor, string? triggeredBy, CancellationToken ct = default)
        => await db.CollectTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct) is { } task
            ? await EnqueueAsync(task, trigger, scheduledFor, triggeredBy, ct)
            : EnqueueResult.Fail("采集任务不存在");

    public async Task<EnqueueResult> TriggerAsync(long taskId, string triggeredBy, bool anyTargetType, CancellationToken ct = default)
    {
        if (await db.CollectTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct) is not { } task)
        {
            return EnqueueResult.Fail("采集任务不存在");
        }

        if (!anyTargetType && !CollectTaskRule.IsEditorTriggerable(task.TargetType))
        {
            return EnqueueResult.Fail($"只能立即执行日历排期、公布值类的任务，{task.Name} 的目标数据类型为{CollectDisplayNames.Get(task.TargetType)}");
        }

        return await EnqueueAsync(task, CollectRunTrigger.Manual, scheduledFor: null, triggeredBy, ct);
    }

    private async Task<EnqueueResult> EnqueueAsync(
        CollectTask task, CollectRunTrigger trigger, DateTime? scheduledFor, string? triggeredBy, CancellationToken ct)
    {
        var source = await db.DataSources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == task.SourceId, ct);
        if (source is not { IsEnabled: true })
        {
            return EnqueueResult.Fail($"数据源 {source?.Name} 已停用，不能执行");
        }

        if (!task.IsEnabled && trigger != CollectRunTrigger.Manual)
        {
            return EnqueueResult.Fail($"采集任务 {task.Name} 已停用");
        }

        var run = new CollectRun
        {
            TaskId = task.Id,
            SourceId = task.SourceId,
            Trigger = trigger,
            ScheduledFor = scheduledFor,
            TriggeredBy = triggeredBy,
            Status = CollectRunStatus.Queued,
            QueuedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.CollectRuns.Add(run);
        await db.SaveChangesAsync(ct);

        // 不做 Outbox（后端约定 §8.2）：发布失败时记录停在"排队"，由调度分发重新发布，这里不让整个操作失败
        try
        {
            await eventBus.PublishAsync(new CollectRunCommand(run.Id, Attempt: 1), ct: ct);
            return new EnqueueResult(true, null, run.Id, Published: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "运行记录 {RunId} 已创建，下发 CollectRunCommand 失败，等待调度分发重新发布", run.Id);
            return new EnqueueResult(true, null, run.Id, Published: false);
        }
    }
}
