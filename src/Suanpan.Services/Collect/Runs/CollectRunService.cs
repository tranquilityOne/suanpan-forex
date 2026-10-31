using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Data;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Services.Collect.Runs.Models;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Collect.Tasks;
using Suanpan.Services.Common.Querying;
using Suanpan.Shared.Messages;
using Suanpan.Shared.Messages.Collect;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Collect.Runs;

public sealed class CollectRunService(
    SuanpanDbContext db,
    IParserCatalog parserCatalog,
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

    public async Task<CollectRunDetail?> GetAsync(long id, CancellationToken ct = default)
    {
        var item = await (
            from r in db.CollectRuns.AsNoTracking()
            where r.Id == id
            join t in db.CollectTasks on r.TaskId equals t.Id
            join s in db.DataSources on r.SourceId equals s.Id into sources
            from s in sources.DefaultIfEmpty()
            select new { Run = r, TaskName = t.Name, t.TargetType, SourceName = s == null ? null : s.Name })
            .FirstOrDefaultAsync(ct);
        if (item is null)
        {
            return null;
        }

        var snapshots = await db.Snapshots.AsNoTracking()
            .Where(s => s.RunId == id)
            .OrderBy(s => s.Attempt).ThenBy(s => s.Seq)
            .Select(s => new RunSnapshot(s.Id, s.Attempt, s.Seq, s.Url, s.HttpStatus, s.Size, s.Unchanged, s.IsValid))
            .ToListAsync(ct);
        return new CollectRunDetail(item.Run, item.TaskName, item.TargetType, item.SourceName, snapshots);
    }

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

        var queuedAt = timeProvider.GetUtcNow().UtcDateTime;
        long runId;
        if (scheduledFor is { } point)
        {
            // 定时触发：同一任务同一时间点只创建一次（ux_col_run_task_id_trigger_scheduled_for），调度分发重复执行或中途退出后重跑，
            // 都不会重复下发（采集设计 §4.2，后端约定 §6.4）
            var inserted = await db.Database.SqlQuery<long>($"""
                INSERT INTO col_run (task_id, source_id, "trigger", scheduled_for, triggered_by, status, queued_at,
                                     attempt_count, inserted_count, updated_count, skipped_count, attempts)
                VALUES ({task.Id}, {task.SourceId}, {(short)trigger}, {point}, {triggeredBy}, {(short)CollectRunStatus.Queued}, {queuedAt},
                        0, 0, 0, 0, '[]'::jsonb)
                ON CONFLICT (task_id, "trigger", scheduled_for) WHERE scheduled_for IS NOT NULL DO NOTHING
                RETURNING id AS "Value"
                """).ToListAsync(ct);
            if (inserted.Count == 0)
            {
                return EnqueueResult.Fail($"采集任务 {task.Name} 在这个时间点的运行记录已经创建");
            }

            runId = inserted[0];
        }
        else
        {
            var run = new CollectRun
            {
                TaskId = task.Id,
                SourceId = task.SourceId,
                Trigger = trigger,
                TriggeredBy = triggeredBy,
                Status = CollectRunStatus.Queued,
                QueuedAt = queuedAt,
            };
            db.CollectRuns.Add(run);
            await db.SaveChangesAsync(ct);
            runId = run.Id;
        }

        // 不做 Outbox（后端约定 §8.2）：发布失败时记录停在"排队"，由调度分发重新发布，这里不让整个操作失败
        var published = await TryPublishAsync(runId, trigger, ct);
        return new EnqueueResult(true, null, runId, published);
    }

    public async Task<bool> TryPublishAsync(long runId, CollectRunTrigger trigger, CancellationToken ct = default)
    {
        IntegrationMessage command = trigger == CollectRunTrigger.Reparse ? new ReparseCommand(runId) : new CollectRunCommand(runId, Attempt: 1);
        try
        {
            await eventBus.PublishAsync(command, ct: ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "运行记录 {RunId} 下发 {Command} 失败，等待调度分发重新发布", runId, command.GetType().Name);
            return false;
        }
    }

    public Task<int> CountForReparseAsync(ReparseSelection selection, CancellationToken ct = default)
        => selection.Filter.TaskId is null ? Task.FromResult(0) : SelectedSnapshots(selection).CountAsync(ct);

    public async Task<EnqueueResult> ReparseAsync(ReparseSelection selection, string parserKey, string triggeredBy, CancellationToken ct = default)
    {
        if (selection.Filter.TaskId is not { } taskId || await db.CollectTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct) is not { } task)
        {
            return EnqueueResult.Fail("采集任务不存在");
        }

        if (ReparseRule.ValidateParser(parserCatalog.Find(task.ParserKey), parserCatalog.Find(parserKey)) is { } parserError)
        {
            return EnqueueResult.Fail(parserError);
        }

        var snapshots = SelectedSnapshots(selection);
        if (ReparseRule.ValidateCount(await snapshots.CountAsync(ct)) is { } countError)
        {
            return EnqueueResult.Fail(countError);
        }

        var run = new CollectRun
        {
            TaskId = task.Id,
            SourceId = task.SourceId,
            Trigger = CollectRunTrigger.Reparse,
            TriggeredBy = triggeredBy,
            Status = CollectRunStatus.Queued,
            QueuedAt = timeProvider.GetUtcNow().UtcDateTime,
            Reparse = new CollectRunReparse
            {
                ParserKey = parserKey,
                SnapshotIds = await snapshots.OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(ct),
            },
        };
        db.CollectRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var published = await TryPublishAsync(run.Id, CollectRunTrigger.Reparse, ct);
        return new EnqueueResult(true, null, run.Id, published);
    }

    /// <summary>按快照列表的筛选条件取任务的快照；勾选了快照时只取其中的</summary>
    private IQueryable<Snapshot> SelectedSnapshots(ReparseSelection selection)
    {
        var snapshots = db.Snapshots.AsNoTracking().Filter(db, selection.Filter);
        if (selection.SnapshotIds.Count > 0)
        {
            var ids = selection.SnapshotIds.ToList();
            snapshots = snapshots.Where(s => ids.Contains(s.Id));
        }

        return snapshots;
    }
}
