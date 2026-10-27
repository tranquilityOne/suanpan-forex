using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Data;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Tasks;

namespace Suanpan.Services.Collect.Jobs;

/// <summary>
/// 采集任务的调度分发（采集设计 §4.2，FR-002）：Job 中每分钟执行一次的周期任务 collect-dispatch。
/// <list type="number">
/// <item>启用的、有 cron 的、所属数据源也启用的任务，算出 (last_dispatched_at, 现在] 之间最后一个时间点，创建运行记录并下发；
/// 错过的多个时间点只补最近一次。同一时间点只创建一次（唯一索引兜底），重复执行不会重复下发</item>
/// <item>下发失败的兜底：排队 2 ~ 3 分钟的记录重新发布一次；排队超过 10 分钟仍未开始的记为失败（ERR-COL-008）</item>
/// </list>
/// 多个 Job 实例时用分布式锁保证同一时刻只有一个在分发（后端约定 §8.3）
/// </summary>
public sealed class CollectDispatchJob(
    SuanpanDbContext db,
    ICollectRunService runs,
    IDistributedLock distributedLock,
    TimeProvider timeProvider,
    ILogger<CollectDispatchJob> logger)
{
    public const string JobId = "collect-dispatch";

    /// <summary>每分钟（UTC）</summary>
    public const string Cron = "* * * * *";

    /// <summary>排队超过这个时长还没开始，重新发布一次</summary>
    public static readonly TimeSpan RepublishAfter = TimeSpan.FromMinutes(2);

    /// <summary>排队超过这个时长还没开始，记为失败（下发失败）</summary>
    public static readonly TimeSpan FailAfter = TimeSpan.FromMinutes(10);

    private const string LockKey = "collect:dispatch";

    /// <summary>一轮分发通常在几秒内完成；锁过期时间留足余量，又不至于在进程崩溃后卡住下一轮太久</summary>
    private static readonly TimeSpan LockExpiry = TimeSpan.FromMinutes(5);

    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken ct)
    {
        await using var handle = await distributedLock.AcquireAsync(LockKey, LockExpiry, ct: ct);
        if (handle is null)
        {
            logger.LogInformation("另一个实例正在分发采集任务，跳过本轮");
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await DispatchDueTasksAsync(now, ct);
        await RepublishQueuedAsync(now, ct);
        await FailStaleQueuedAsync(now, ct);
    }

    private async Task DispatchDueTasksAsync(DateTime now, CancellationToken ct)
    {
        var tasks = await (
            from t in db.CollectTasks.AsNoTracking()
            join s in db.DataSources on t.SourceId equals s.Id
            where t.IsEnabled && t.Cron != null && s.IsEnabled
            select new { t.Id, t.Name, Cron = t.Cron!, t.LastDispatchedAt })
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            try
            {
                if (task.LastDispatchedAt is { } last)
                {
                    if (CronSchedule.LatestOccurrence(task.Cron, last, now) is not { } point)
                    {
                        continue;
                    }

                    var result = await runs.EnqueueAsync(task.Id, CollectRunTrigger.Scheduled, point, triggeredBy: null, ct);
                    if (!result.Success)
                    {
                        logger.LogInformation("采集任务 {TaskId} 在 {ScheduledFor:u} 没有下发：{Reason}", task.Id, point, result.Error);
                    }
                }

                // 下发后再记账，中途退出时下一轮会算出同一个时间点，由唯一索引去重；
                // 没有记账时间（不经后台启用的任务）时只记账，从现在开始算，不补发
                await db.CollectTasks.Where(t => t.Id == task.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.LastDispatchedAt, now), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 一个任务出错（如 cron 被改坏）不影响其他任务，下一轮重试
                logger.LogError(ex, "采集任务 {TaskId}（{TaskName}）分发失败", task.Id, task.Name);
            }
        }
    }

    /// <summary>
    /// 排队满 2 分钟的记录重新发布一次：按整分钟划分窗口，第 n 分钟的这一轮只处理在 [n-3, n-2) 分钟内排队的记录，
    /// 每分钟一轮时每条记录正好落在一个窗口里。偶尔重复发布由 Collector 的幂等处理兜住
    /// </summary>
    private async Task RepublishQueuedAsync(DateTime now, CancellationToken ct)
    {
        var minute = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var to = minute - RepublishAfter;
        var from = to - TimeSpan.FromMinutes(1);
        var ids = await db.CollectRuns.AsNoTracking()
            .Where(r => r.Status == CollectRunStatus.Queued && r.QueuedAt >= from && r.QueuedAt < to)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            if (await runs.TryPublishAsync(id, ct))
            {
                logger.LogWarning("运行记录 {RunId} 排队超过 {Minutes} 分钟仍未开始，已重新下发", id, RepublishAfter.TotalMinutes);
            }
        }
    }

    /// <summary>排队超过 10 分钟仍未开始的记为失败（ERR-COL-008）；条件更新，Collector 同时开始执行的那条不会被改写</summary>
    private async Task FailStaleQueuedAsync(DateTime now, CancellationToken ct)
    {
        var cutoff = now - FailAfter;
        var message = $"排队超过 {FailAfter.TotalMinutes:0} 分钟仍未开始，可能是下发失败或采集服务没有运行";
        var stale = db.CollectRuns.Where(r => r.Status == CollectRunStatus.Queued && r.QueuedAt < cutoff);
        var ids = await stale.Select(r => r.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return;
        }

        var failed = await stale.Where(r => ids.Contains(r.Id)).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Status, CollectRunStatus.Failed)
            .SetProperty(r => r.EndedAt, now)
            .SetProperty(r => r.ErrorCode, CollectErrorCodes.DispatchFailed)
            .SetProperty(r => r.ErrorMessage, message), ct);

        // 告警由 FR-025 按 BR-129 的规则处理，这里先记 Error 日志
        logger.LogError("{Count} 条运行记录排队超过 {Minutes} 分钟仍未开始，记为失败（{ErrorCode}）：{RunIds}",
            failed, FailAfter.TotalMinutes, CollectErrorCodes.DispatchFailed, ids);
    }
}
