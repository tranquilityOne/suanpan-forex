using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Services.Calendar.Schedules;
using Suanpan.Services.Calendar.Schedules.Models;
using Suanpan.Services.Calendar.Unmatched.Models;
using Suanpan.Services.Common.Querying;
using Suanpan.Shared.Messages.Calendar;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Calendar.Unmatched;

public sealed class UnmatchedEntryService(
    SuanpanDbContext db,
    ScheduleApplier applier,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<UnmatchedEntryService> logger) : IUnmatchedEntryService
{
    public async Task<PagedResult<UnmatchedReleaseGroup>> QueryAsync(UnmatchedEntryQuery query, CancellationToken ct = default)
    {
        var entries = db.UnmatchedEntries.AsNoTracking();
        if (query.SourceId is { } sourceId)
        {
            entries = entries.Where(u => u.SourceId == sourceId);
        }

        var page = await entries
            .GroupBy(u => new { u.SourceId, u.ReleaseKey })
            .Select(g => new { g.Key.SourceId, g.Key.ReleaseKey, FirstDate = g.Min(u => u.LocalDate) })
            .OrderBy(g => g.FirstDate).ThenBy(g => g.SourceId).ThenBy(g => g.ReleaseKey)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
        if (page.Items.Count == 0)
        {
            return new PagedResult<UnmatchedReleaseGroup>([], page.Total, page.Page, page.PageSize);
        }

        var sourceIds = page.Items.Select(g => g.SourceId).Distinct().ToList();
        var keys = page.Items.Select(g => g.ReleaseKey).Distinct().ToList();
        var rows = await entries.Where(u => sourceIds.Contains(u.SourceId) && keys.Contains(u.ReleaseKey)).ToListAsync(ct);
        var sources = await SourcesAsync(sourceIds, ct);
        var groups = page.Items.Select(g =>
        {
            var groupRows = rows.Where(u => u.SourceId == g.SourceId && u.ReleaseKey == g.ReleaseKey).ToList();
            var latest = groupRows.MaxBy(u => u.LastSeenAt)!;
            var source = sources.GetValueOrDefault(g.SourceId);
            return new UnmatchedReleaseGroup(
                g.SourceId, source?.Name ?? $"#{g.SourceId}", source?.TimeZone, g.ReleaseKey, latest.ReleaseName, latest.LastSeenAt,
                groupRows
                    .OrderBy(u => u.ReferencePeriod, StringComparer.Ordinal)
                    .Select(u => new UnmatchedEntryRow(u.ReferencePeriod, u.LocalDate, u.LocalTime, u.TimePrecision))
                    .ToList());
        }).ToList();
        return new PagedResult<UnmatchedReleaseGroup>(groups, page.Total, page.Page, page.PageSize);
    }

    public Task<PagedResult<IgnoredReleaseRow>> QueryIgnoredAsync(IgnoredReleaseQuery query, CancellationToken ct = default)
    {
        var ignored = db.IgnoredReleases.AsNoTracking();
        if (query.SourceId is { } sourceId)
        {
            ignored = ignored.Where(i => i.SourceId == sourceId);
        }

        return (from i in ignored
                join s in db.DataSources on i.SourceId equals s.Id
                orderby s.Name, i.ReleaseKey
                select new IgnoredReleaseRow(i.Id, i.SourceId, s.Name, i.ReleaseKey, i.ReleaseName, i.CreatedBy, i.CreatedAt))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
    }

    public async Task<ManageResult> IgnoreAsync(long sourceId, string releaseKey, CancellationToken ct = default)
    {
        var entries = await db.UnmatchedEntries.Where(u => u.SourceId == sourceId && u.ReleaseKey == releaseKey).ToListAsync(ct);
        var alreadyIgnored = await db.IgnoredReleases.AnyAsync(i => i.SourceId == sourceId && i.ReleaseKey == releaseKey, ct);
        if (entries.Count == 0 && !alreadyIgnored)
        {
            return ManageResult.Fail("未匹配条目不存在，可能已被处理或已过期");
        }

        if (!alreadyIgnored)
        {
            db.IgnoredReleases.Add(new IgnoredRelease
            {
                SourceId = sourceId,
                ReleaseKey = releaseKey,
                ReleaseName = entries.MaxBy(u => u.LastSeenAt)!.ReleaseName,
            });
        }

        db.UnmatchedEntries.RemoveRange(entries);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> RestoreAsync(long id, CancellationToken ct = default)
    {
        var deleted = await db.IgnoredReleases.Where(i => i.Id == id).ExecuteDeleteAsync(ct);
        return deleted > 0 ? ManageResult.Ok : ManageResult.Fail("忽略记录不存在，可能已被恢复");
    }

    public async Task<int> RematchAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var mappings = await (
            from m in db.IndicatorSources.AsNoTracking()
            where m.Purpose == IndicatorSourcePurpose.Schedule
            join i in db.Indicators on m.IndicatorCode equals i.Code
            join e in db.Economies on i.EconomyCode equals e.Code
            select new { m.SourceId, m.ExternalId, i.Code, i.Frequency, e.TimeZone, i.IsEnabled }).ToListAsync(ct);

        // 数据源 + 发布标识 → 启用的指标；有映射但指标都已停用的标识对应空列表（条目照样删除，与排期入库的清理一致）
        var indicatorsByKey = mappings
            .GroupBy(m => (m.SourceId, m.ExternalId))
            .ToDictionary(g => g.Key, g => g.Where(m => m.IsEnabled).Select(m => new ScheduleIndicator(m.Code, m.Frequency, m.TimeZone)).ToList());
        var messages = new List<EventRescheduledEvent>();
        var matched = 0;
        await db.InTransactionAsync(async () =>
        {
            var entries = await db.UnmatchedEntries.OrderBy(u => u.Id).ToListAsync(ct);
            foreach (var entry in entries)
            {
                if (!indicatorsByKey.TryGetValue((entry.SourceId, entry.ReleaseKey), out var indicators))
                {
                    continue;
                }

                db.UnmatchedEntries.Remove(entry);
                if (indicators.Count > 0)
                {
                    matched++;
                    await ApplyAsync(entry, indicators, now, messages, ct);
                }
            }

            await db.SaveChangesAsync(ct);
        }, ct);

        foreach (var message in messages)
        {
            await PublishAsync(message, ct);
        }

        return matched;
    }

    /// <summary>与排期入库相同的写法（§4.2、§4.3）：每个指标按自己的时区换算排期，已经过去的不建事件；变更历史不带运行记录</summary>
    private async Task ApplyAsync(
        UnmatchedEntry entry, List<ScheduleIndicator> indicators, DateTime now, List<EventRescheduledEvent> messages, CancellationToken ct)
    {
        var batchId = ScheduleTarget.BatchId(entry.SourceId, entry.ReleaseKey, entry.LocalDate);
        foreach (var indicator in indicators)
        {
            if (ReferencePeriodRule.Validate(entry.ReferencePeriod, indicator.Frequency) is { } periodError)
            {
                logger.LogWarning("未匹配条目 {ReleaseKey} {ReferencePeriod} 重新匹配到 {IndicatorCode}，参考期不符：{Reason}",
                    entry.ReleaseKey, entry.ReferencePeriod, indicator.Code, periodError);
                continue;
            }

            var schedule = ScheduleRule.Resolve(entry.LocalDate, entry.LocalTime, entry.TimePrecision, indicator.TimeZone);
            if (!ScheduleRule.IsFuture(schedule, entry.TimePrecision, indicator.TimeZone, now))
            {
                continue;
            }

            var result = await applier.ApplyAsync(new ScheduleTarget(
                indicator, entry.ReferencePeriod, schedule, entry.TimePrecision, entry.SourceId, entry.ReleaseKey, batchId, RunId: null), ct);
            if (result.Rescheduled is { } message)
            {
                messages.Add(message);
            }
        }
    }

    /// <summary>数据源的名称和所属经济体的时区</summary>
    private async Task<Dictionary<long, SourceInfo>> SourcesAsync(List<long> sourceIds, CancellationToken ct)
        => await (
            from s in db.DataSources.AsNoTracking()
            where sourceIds.Contains(s.Id)
            join e in db.Economies on s.EconomyCode equals e.Code into economies
            from e in economies.DefaultIfEmpty()
            select new SourceInfo(s.Id, s.Name, e == null ? null : e.TimeZone)).ToDictionaryAsync(s => s.Id, ct);

    private async Task PublishAsync(EventRescheduledEvent message, CancellationToken ct)
    {
        try
        {
            await eventBus.PublishAsync(message, ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "事件 {EventId} 的改期消息发布失败", message.EventId);
        }
    }

    private sealed record SourceInfo(long Id, string Name, string? TimeZone);
}
