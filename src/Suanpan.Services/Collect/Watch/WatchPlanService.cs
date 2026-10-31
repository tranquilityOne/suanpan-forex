using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Ops.Enums;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Watch.Models;
using Suanpan.Services.Common.Alerts;
using Suanpan.Services.Sys.Configs;

namespace Suanpan.Services.Collect.Watch;

public sealed class WatchPlanService(
    SuanpanDbContext db,
    IConfigService config,
    IRedisCache cache,
    IAlertService alerts,
    ILogger<WatchPlanService> logger) : IWatchPlanService
{
    private const string UnreadyKeyPrefix = "collect:watch:unready:";

    private const int RecentLimit = 50;

    private static readonly TimeSpan UnreadyAlertExpiry = TimeSpan.FromHours(24);

    public async Task<WatchSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        var enabled = await config.GetBoolAsync(CollectConfigKeys.WatchEnabled, CollectConfigKeys.WatchEnabledDefault, ct);
        var includeMedium = await config.GetBoolAsync(CollectConfigKeys.WatchIncludeMedium, CollectConfigKeys.WatchIncludeMediumDefault, ct);
        var defaults = new WatchDefaults(
            await ReadIntAsync(CollectConfigKeys.WatchLeadSeconds, WatchRule.MinLeadSeconds, WatchRule.MaxLeadSeconds, WatchRule.DefaultLeadSeconds, ct),
            await ReadIntAsync(CollectConfigKeys.WatchWindowMinutes, WatchRule.MinWindowMinutes, WatchRule.MaxWindowMinutes, WatchRule.DefaultWindowMinutes, ct),
            await ReadIntAsync(CollectConfigKeys.WatchIntervalSeconds, WatchRule.MinIntervalSeconds, WatchRule.MaxIntervalSeconds,
                WatchRule.DefaultIntervalSeconds, ct));
        var planMinutes = await ReadIntAsync(CollectConfigKeys.WatchPlanMinutes, WatchRule.MinPlanMinutes, WatchRule.MaxPlanMinutes, WatchRule.DefaultPlanMinutes, ct);
        var backfill = new BackfillSettings(
            await ReadIntAsync(CollectConfigKeys.WatchBackfillMinutes, WatchRule.MinBackfillMinutes, WatchRule.MaxBackfillMinutes,
                WatchRule.DefaultBackfillMinutes, ct),
            await ReadIntAsync(CollectConfigKeys.WatchBackfillHours, 1, WatchRule.MaxBackfillHours, WatchRule.DefaultBackfillHours, ct),
            await ReadIntAsync(CollectConfigKeys.WatchBackfillSlowMinutes, WatchRule.MinBackfillMinutes, WatchRule.MaxBackfillMinutes,
                WatchRule.DefaultBackfillSlowMinutes, ct));
        return new WatchSettings(enabled, includeMedium, defaults, planMinutes, backfill);
    }

    public async Task<WatchPlan> GetPlanAsync(DateTime begin, DateTime end, CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        Importance[] importances = settings.IncludeMedium ? [Importance.High, Importance.Medium] : [Importance.High];
        var events = await (
                from e in db.CalendarEvents.AsNoTracking()
                where (e.Status == CalendarEventStatus.Scheduled || e.Status == CalendarEventStatus.Watching)
                    && e.TimePrecision == TimePrecision.Minute && e.ScheduledAt >= begin && e.ScheduledAt <= end
                join i in db.Indicators on e.IndicatorCode equals i.Code
                where i.IsEnabled && importances.Contains(i.Importance)
                orderby e.ScheduledAt, e.Id
                select new { e.Id, e.IndicatorCode, i.ShortName, i.Importance, e.ReferencePeriod, e.Status, e.ScheduledAt })
            .ToListAsync(ct);

        var targets = await WatchTargets.LoadAsync(db, events.Select(e => e.IndicatorCode).Distinct().ToList(), ct);
        var problems = new List<WatchProblem>();
        var members = new List<(WatchEventItem Item, DateTime ScheduledAt, DataSource Source, CollectTask Task, short? Lead, short? Window)>();
        foreach (var e in events)
        {
            var item = new WatchEventItem(e.Id, e.IndicatorCode, e.ShortName, e.Importance, e.ReferencePeriod, e.Status);
            var scheduledAt = e.ScheduledAt!.Value;
            var target = targets.Resolve(e.IndicatorCode);
            if (target.Kind is { } kind)
            {
                problems.Add(new WatchProblem(item, scheduledAt, kind, target.Reason!));
                continue;
            }

            members.Add((item, scheduledAt, target.Source!, target.Task!, target.Mapping!.WatchLeadSeconds, target.Mapping.WatchWindowMinutes));
        }

        var groups = members
            .GroupBy(m => (SourceId: m.Source.Id, TaskId: m.Task.Id, m.ScheduledAt))
            .Select(g =>
            {
                var first = g.First();
                var timing = WatchRule.Merge(g.Select(m => (m.Lead, m.Window)), first.Source.WatchIntervalSeconds, first.Source.RateLimit,
                    first.Source.RateLimitUnit, settings.Defaults);
                return new WatchGroup(first.Source.Id, first.Source.Name, first.Task.Id, first.Task.Name, g.Key.ScheduledAt, timing,
                    WatchRule.Window(g.Key.ScheduledAt, timing), g.Select(m => m.Item).ToList());
            })
            .OrderBy(g => g.Window.Start).ThenBy(g => g.SourceName, StringComparer.Ordinal)
            .ToList();
        return new WatchPlan(settings, groups, problems);
    }

    public async Task AlertUnreadyAsync(IEnumerable<WatchProblem> problems, CancellationToken ct = default)
    {
        foreach (var problem in problems)
        {
            var e = problem.Event;
            try
            {
                if (!await cache.SetIfNotExistsAsync(UnreadyKeyPrefix + e.EventId, true, UnreadyAlertExpiry))
                {
                    continue;
                }

                await alerts.RaiseAsync(AlertLevel.Important, e.IndicatorCode,
                    $"{e.IndicatorCode} {e.ReferencePeriod} 将于 {problem.ScheduledAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC 公布，"
                    + $"但不能盯盘：{problem.Reason}", ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 告警是附带动作，失败只记日志（后端约定 §11）
                logger.LogWarning(ex, "事件 {EventId} 不能盯盘的告警发送失败", e.EventId);
            }
        }
    }

    public async Task<IReadOnlyList<WatchRecentRun>> GetRecentAsync(DateTime since, CancellationToken ct = default)
    {
        var runs = await (
                from r in db.CollectRuns.AsNoTracking()
                where r.Trigger == CollectRunTrigger.Watch && r.ScheduledFor != null && r.EndedAt >= since
                join s in db.DataSources on r.SourceId equals s.Id into sources
                from s in sources.DefaultIfEmpty()
                orderby r.EndedAt descending, r.Id descending
                select new { Run = r, SourceName = s == null ? null : s.Name })
            .Take(RecentLimit)
            .ToListAsync(ct);
        if (runs.Count == 0)
        {
            return [];
        }

        // 盯盘开始时日历服务把事件改为"盯盘中"，状态历史带运行记录：据此找出每一路盯了哪些事件
        var runIds = runs.Select(x => (long?)x.Run.Id).ToList();
        var watching = EventChanges.Status(CalendarEventStatus.Watching);
        var links = (await db.CalendarEventChanges.AsNoTracking()
                .Where(c => runIds.Contains(c.RunId) && c.Kind == EventChangeKind.Status)
                .Select(c => new { RunId = c.RunId!.Value, c.EventId, c.NewValue })
                .ToListAsync(ct))
            .Where(c => c.NewValue == watching)
            .Select(c => (c.RunId, c.EventId))
            .Distinct()
            .ToList();
        var eventIds = links.Select(l => l.EventId).Distinct().ToList();
        var events = await (
                from e in db.CalendarEvents.AsNoTracking()
                where eventIds.Contains(e.Id)
                join i in db.Indicators on e.IndicatorCode equals i.Code
                select new { e.Id, e.IndicatorCode, i.ShortName, e.ReferencePeriod, e.Status, e.DetectedAt })
            .ToDictionaryAsync(e => e.Id, ct);

        return runs
            .Select(x =>
            {
                var scheduledAt = x.Run.ScheduledFor!.Value;
                var items = links.Where(l => l.RunId == x.Run.Id && events.ContainsKey(l.EventId))
                    .Select(l => events[l.EventId])
                    .OrderBy(e => e.IndicatorCode, StringComparer.Ordinal)
                    .Select(e => new WatchRecentEvent(e.Id, e.IndicatorCode, e.ShortName, e.ReferencePeriod, e.Status, e.DetectedAt,
                        e.DetectedAt is { } detected ? detected - scheduledAt : null))
                    .ToList();
                return new WatchRecentRun(x.Run.Id, x.Run.SourceId, x.SourceName, scheduledAt, x.Run.Status, x.Run.EndedAt, x.Run.AttemptCount,
                    x.Run.ErrorMessage, items);
            })
            .ToList();
    }

    /// <summary>整数配置：超出范围时按默认值并记警告日志</summary>
    private async Task<int> ReadIntAsync(string key, int min, int max, int defaultValue, CancellationToken ct)
    {
        var value = await config.GetIntAsync(key, defaultValue, ct);
        if (WatchRule.OrDefault(value, min, max, defaultValue) == value)
        {
            return value;
        }

        logger.LogWarning("系统配置 {Key} 的值 {Value} 不在 {Min} ~ {Max} 之间，按默认值 {Default} 处理", key, value, min, max, defaultValue);
        return defaultValue;
    }
}
