using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Ops.Enums;
using Suanpan.Data;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Common.Alerts;

namespace Suanpan.Services.Collect.Watch;

public sealed class WatchBackfillService(
    SuanpanDbContext db,
    ICollectRunService runs,
    IRedisCache cache,
    IAlertService alerts,
    TimeProvider timeProvider,
    ILogger<WatchBackfillService> logger) : IWatchBackfillService
{
    private const string SlowKeyPrefix = "collect:watch:slow:";

    /// <summary>转为慢间隔的告警每个事件只发一次：键保留 30 天，延迟这么久的事件早该人工处理了</summary>
    private static readonly TimeSpan SlowAlertExpiry = TimeSpan.FromDays(30);

    private static readonly CollectRunStatus[] ActiveStatuses = [CollectRunStatus.Queued, CollectRunStatus.Running];

    public async Task<IReadOnlyList<long>> RunAsync(BackfillSettings settings, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // 延迟的事件：盯盘 T+10 分钟无值，或编辑驳回待复核后继续补采（PRD §8.1.3）
        var delayed = await (
                from e in db.CalendarEvents.AsNoTracking()
                where e.Status == CalendarEventStatus.Delayed
                join i in db.Indicators on e.IndicatorCode equals i.Code
                where i.IsEnabled
                select new { e.Id, e.IndicatorCode, e.ReferencePeriod })
            .ToListAsync(ct);
        if (delayed.Count == 0)
        {
            return [];
        }

        // 变为延迟的时间：最近一次改为"延迟"的状态历史
        var ids = delayed.Select(e => e.Id).ToList();
        var delayedJson = EventChanges.Status(CalendarEventStatus.Delayed);
        var delayedAt = (await db.CalendarEventChanges.AsNoTracking()
                .Where(c => ids.Contains(c.EventId) && c.Kind == EventChangeKind.Status)
                .Select(c => new { c.EventId, c.NewValue, c.ChangedAt })
                .ToListAsync(ct))
            .Where(c => c.NewValue == delayedJson)
            .GroupBy(c => c.EventId)
            .ToDictionary(g => g.Key, g => g.Max(c => c.ChangedAt));

        var targets = await WatchTargets.LoadAsync(db, delayed.Select(e => e.IndicatorCode).Distinct().ToList(), ct);
        var byTask = new Dictionary<long, TimeSpan>();
        foreach (var e in delayed)
        {
            var since = delayedAt.GetValueOrDefault(e.Id, now);
            if (now - since >= TimeSpan.FromHours(settings.Hours))
            {
                await AlertSlowAsync(e.Id, e.IndicatorCode, e.ReferencePeriod, settings, ct);
            }

            // 找不到数据源或任务的不补采：不能盯盘的原因已在计划时告警
            if (targets.Resolve(e.IndicatorCode).Task is not { } task)
            {
                continue;
            }

            var interval = WatchRule.BackfillInterval(since, now, settings);
            byTask[task.Id] = byTask.TryGetValue(task.Id, out var current) && current < interval ? current : interval;
        }

        var taskIds = byTask.Keys.ToList();
        var lastRuns = await db.CollectRuns.AsNoTracking()
            .Where(r => taskIds.Contains(r.TaskId))
            .GroupBy(r => r.TaskId)
            .Select(g => new { TaskId = g.Key, LastQueuedAt = g.Max(r => r.QueuedAt), Active = g.Any(r => ActiveStatuses.Contains(r.Status)) })
            .ToDictionaryAsync(x => x.TaskId, ct);

        var created = new List<long>();
        foreach (var (taskId, interval) in byTask)
        {
            var last = lastRuns.GetValueOrDefault(taskId);
            if (last is { Active: true } || !WatchRule.NeedsBackfill(last?.LastQueuedAt, interval, now))
            {
                continue;
            }

            var result = await runs.EnqueueAsync(taskId, CollectRunTrigger.Watch, scheduledFor: null, triggeredBy: null, ct);
            if (result.RunId is { } runId)
            {
                created.Add(runId);
            }
            else
            {
                logger.LogWarning("延迟事件的补采没有创建运行记录（任务 {TaskId}）：{Error}", taskId, result.Error);
            }
        }

        return created;
    }

    /// <summary>延迟超过快间隔的时长、转为慢间隔时告警一次（BR-017、BR-129）；Redis 或告警失败只记日志</summary>
    private async Task AlertSlowAsync(long eventId, string indicatorCode, string referencePeriod, BackfillSettings settings, CancellationToken ct)
    {
        try
        {
            if (await cache.SetIfNotExistsAsync(SlowKeyPrefix + eventId, true, SlowAlertExpiry))
            {
                await alerts.RaiseAsync(AlertLevel.Important, indicatorCode,
                    $"{indicatorCode} {referencePeriod} 延迟已超过 {settings.Hours} 小时仍未拿到公布值，补采改为每 {settings.SlowMinutes} 分钟一次，请人工确认", ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "事件 {EventId} 转为慢补采的告警发送失败", eventId);
        }
    }
}
