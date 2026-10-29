using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Records;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Schedules.Models;
using Suanpan.Services.Collect.Ingest;
using Suanpan.Shared.Messages.Calendar;

namespace Suanpan.Services.Calendar.Schedules;

/// <summary>
/// 排期入库（FR-007，日历设计 §4）：排期解析器输出的 <see cref="ScheduleEntryRecord"/> 按"排期"用途的映射（BR-034、BR-156）
/// 建成或更新日历事件。排期只来自数据源（DEC-024）。整个入库在一个事务中完成，同样的条目重复入库结果不变：
/// <list type="number">
/// <item>逐条校验日期时间（ERR-PARSE-005），只处理未来的条目（§4.2）</item>
/// <item>没有映射：发布标识已忽略的跳过，否则写入未匹配条目（ERR-EVT-003，§4.5）</item>
/// <item>有映射：对每个启用的指标校验参考期，交给 <see cref="ScheduleApplier"/></item>
/// <item>本次出现过的发布标识下，本数据源维护、未来的"待公布"事件不在条目中的，改为"待确认"（BR-040，§4.4）</item>
/// <item>清理本数据源的未匹配条目：日期已过的、发布标识已有映射的</item>
/// </list>
/// 改期消息（EV-003）在事务提交后发布，发布失败只记日志
/// </summary>
public sealed class CalendarScheduleIngestor(
    SuanpanDbContext db,
    ScheduleApplier applier,
    IAuditActorProvider auditActor,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<CalendarScheduleIngestor> logger) : ICollectIngestor
{
    private const int MaxReleaseKeyLength = 128;

    private const int MaxReleaseNameLength = 256;

    /// <summary>数据源没有所属经济体时，判断未匹配条目是否已过去用的时区</summary>
    private const string FallbackTimeZone = "Etc/UTC";

    public CollectTargetType TargetType => CollectTargetType.Calendar;

    public async Task<IngestResult> IngestAsync(IngestRequest request, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var context = await LoadContextAsync(request.SourceId, ct);
        var counts = new Counts();
        var messages = new List<EventRescheduledEvent>();

        // 本次出现过的发布标识（有映射的），以及条目中出现过的"指标 + 参考期"：判断从日程中消失时用
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var mentioned = new HashSet<(string Code, string Period)>();
        try
        {
            await db.InTransactionAsync(async () =>
            {
                foreach (var record in request.Records)
                {
                    await IngestEntryAsync(request, context, record, now, counts, messages, seenKeys, mentioned, ct);
                }

                counts.Updated += await MarkDisappearedAsync(request, seenKeys, mentioned, now, ct);
                await CleanUpUnmatchedAsync(request.SourceId, context, now, ct);
            }, ct);
        }
        finally
        {
            // 执行器在同一个 DbContext 中继续改运行记录：入库失败回滚后不能留下跟踪中的改动
            db.ChangeTracker.Clear();
        }

        foreach (var message in messages)
        {
            await PublishAsync(message, ct);
        }

        return new IngestResult(counts.Inserted, counts.Updated, counts.Skipped, counts.Failed);
    }

    private async Task IngestEntryAsync(
        IngestRequest request, SourceContext context, object record, DateTime now, Counts counts, List<EventRescheduledEvent> messages,
        HashSet<string> seenKeys, HashSet<(string, string)> mentioned, CancellationToken ct)
    {
        if (record is not ScheduleEntryRecord entry)
        {
            logger.LogError("运行记录 {RunId} 的记录类型 {Type} 不是日程条目，解析器与目标数据类型不符", request.RunId, record.GetType().Name);
            counts.Failed++;
            return;
        }

        var invalid = string.IsNullOrWhiteSpace(entry.ReleaseKey) || entry.ReleaseKey.Length > MaxReleaseKeyLength
            ? "发布标识为空或过长"
            : ScheduleRule.ValidateEntry(entry.LocalDate, entry.LocalTime, entry.Precision);
        if (invalid is not null)
        {
            Reject(request, entry, invalid, counts);
            return;
        }

        var indicators = context.Indicators.GetValueOrDefault(entry.ReleaseKey) ?? [];
        var timeZone = indicators.FirstOrDefault()?.TimeZone ?? context.SourceTimeZone;
        var schedule = ScheduleRule.Resolve(entry.LocalDate, entry.LocalTime, entry.Precision, timeZone);
        if (!ScheduleRule.IsFuture(schedule, entry.Precision, timeZone, now))
        {
            counts.Skipped++;
            return;
        }

        if (!context.MappedKeys.Contains(entry.ReleaseKey))
        {
            await RecordUnmatchedAsync(request, context, entry, now, counts, ct);
            return;
        }

        seenKeys.Add(entry.ReleaseKey);
        if (entry.ReferencePeriod is { } mentionedPeriod)
        {
            foreach (var indicator in indicators)
            {
                mentioned.Add((indicator.Code, mentionedPeriod));
            }
        }

        if (indicators.Count == 0)
        {
            // 映射的指标都已停用：既不建事件，也不算未匹配
            counts.Skipped++;
            return;
        }

        var batchId = entry.LocalDate is { } date ? $"{request.SourceId}:{entry.ReleaseKey}:{date:yyyy-MM-dd}" : null;
        foreach (var indicator in indicators)
        {
            if (ReferencePeriodRule.Validate(entry.ReferencePeriod, indicator.Frequency) is { } periodError)
            {
                Reject(request, entry, $"{indicator.Code}：{periodError}", counts);
                continue;
            }

            var indicatorSchedule = indicator.TimeZone == timeZone
                ? schedule
                : ScheduleRule.Resolve(entry.LocalDate, entry.LocalTime, entry.Precision, indicator.TimeZone);
            var target = new ScheduleTarget(indicator, entry.ReferencePeriod!, indicatorSchedule, entry.Precision,
                request.SourceId, entry.ReleaseKey, batchId, request.RunId);
            var result = await applier.ApplyAsync(target, ct);
            switch (result.Outcome)
            {
                case ScheduleApplyOutcome.Inserted:
                    counts.Inserted++;
                    break;
                case ScheduleApplyOutcome.Updated:
                    counts.Updated++;
                    break;
                default:
                    counts.Skipped++;
                    break;
            }

            if (result.Rescheduled is { } message)
            {
                messages.Add(message);
            }
        }
    }

    /// <summary>没有映射的条目（ERR-EVT-003，§4.5）：发布标识已忽略的跳过，否则按"数据源 + 发布标识 + 参考期"新增或更新</summary>
    private async Task RecordUnmatchedAsync(
        IngestRequest request, SourceContext context, ScheduleEntryRecord entry, DateTime now, Counts counts, CancellationToken ct)
    {
        if (context.IgnoredKeys.Contains(entry.ReleaseKey))
        {
            counts.Skipped++;
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.ReferencePeriod) || entry.ReferencePeriod.Length > 16)
        {
            Reject(request, entry, "缺少参考期或参考期过长", counts);
            return;
        }

        var unmatched = await db.UnmatchedEntries.FirstOrDefaultAsync(u =>
            u.SourceId == request.SourceId && u.ReleaseKey == entry.ReleaseKey && u.ReferencePeriod == entry.ReferencePeriod, ct);
        if (unmatched is null)
        {
            unmatched = new UnmatchedEntry
            {
                SourceId = request.SourceId,
                ReleaseKey = entry.ReleaseKey,
                ReferencePeriod = entry.ReferencePeriod,
                FirstSeenAt = now,
            };
            db.UnmatchedEntries.Add(unmatched);
        }

        unmatched.ReleaseName = entry.ReleaseName.Length > MaxReleaseNameLength ? entry.ReleaseName[..MaxReleaseNameLength] : entry.ReleaseName;
        unmatched.LocalDate = entry.LocalDate;
        unmatched.LocalTime = entry.Precision == TimePrecision.Minute ? entry.LocalTime : null;
        unmatched.TimePrecision = entry.Precision;
        unmatched.LastSeenAt = now;
        unmatched.LastRunId = request.RunId;
        await db.SaveChangesAsync(ct);
        counts.Skipped++;
    }

    /// <summary>
    /// 从日程中消失（BR-040，§4.4）：只检查本次出现过的发布标识，本数据源维护、未来的"待公布"事件不在条目中的改为"待确认"；
    /// 状态被锁定的不改。按 id 顺序行锁读取
    /// </summary>
    private async Task<int> MarkDisappearedAsync(
        IngestRequest request, HashSet<string> seenKeys, HashSet<(string Code, string Period)> mentioned, DateTime now, CancellationToken ct)
    {
        if (seenKeys.Count == 0)
        {
            return 0;
        }

        var keys = seenKeys.ToArray();
        var scheduled = (short)CalendarEventStatus.Scheduled;
        var ids = await db.Database.SqlQuery<long>($"""
            SELECT id AS "Value" FROM cal_event
            WHERE schedule_source_id = {request.SourceId} AND schedule_release_key = ANY({keys})
              AND status = {scheduled} AND scheduled_at > {now}
            ORDER BY id FOR UPDATE
            """).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        var actor = new EventChangeActor(ChangeActorKind.Collector, auditActor.GetActor(), now, request.SourceId, request.RunId);
        var disappeared = (await db.CalendarEvents.Where(e => ids.Contains(e.Id)).ToListAsync(ct))
            .Where(e => !mentioned.Contains((e.IndicatorCode, e.ReferencePeriod)) && !EventLockRule.IsLocked(e.LockedFields, EventField.Status))
            .ToList();
        foreach (var calendarEvent in disappeared)
        {
            db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Status,
                EventChanges.Status(calendarEvent.Status), EventChanges.Status(CalendarEventStatus.Unconfirmed), actor, "从官方日程中消失"));
            calendarEvent.Status = CalendarEventStatus.Unconfirmed;
            calendarEvent.RowVersion++;
        }

        await db.SaveChangesAsync(ct);
        return disappeared.Count;
    }

    /// <summary>未匹配条目只保留未来的：删除日期已过的，以及发布标识现在已有映射的（§4.5）</summary>
    private async Task CleanUpUnmatchedAsync(long sourceId, SourceContext context, DateTime now, CancellationToken ct)
    {
        var today = LocalTimeRule.LocalDate(now, context.SourceTimeZone);
        var mappedKeys = context.MappedKeys.ToArray();
        await db.UnmatchedEntries
            .Where(u => u.SourceId == sourceId && ((u.LocalDate != null && u.LocalDate < today) || mappedKeys.Contains(u.ReleaseKey)))
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// 本数据源"排期"用途的映射（发布标识 → 启用的指标）、有映射的全部发布标识（含指标已停用的）、已忽略的发布标识、
    /// 数据源所属经济体的时区
    /// </summary>
    private async Task<SourceContext> LoadContextAsync(long sourceId, CancellationToken ct)
    {
        var mappings = await (
            from m in db.IndicatorSources.AsNoTracking()
            where m.SourceId == sourceId && m.Purpose == IndicatorSourcePurpose.Schedule
            join i in db.Indicators on m.IndicatorCode equals i.Code
            join e in db.Economies on i.EconomyCode equals e.Code
            select new { m.ExternalId, i.Code, i.Frequency, e.TimeZone, i.IsEnabled }).ToListAsync(ct);
        var indicators = mappings
            .Where(m => m.IsEnabled)
            .GroupBy(m => m.ExternalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(m => new ScheduleIndicator(m.Code, m.Frequency, m.TimeZone)).ToList(), StringComparer.Ordinal);
        var mappedKeys = mappings.Select(m => m.ExternalId).ToHashSet(StringComparer.Ordinal);
        var ignoredKeys = (await db.IgnoredReleases.AsNoTracking().Where(i => i.SourceId == sourceId).Select(i => i.ReleaseKey).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var sourceTimeZone = await (
            from s in db.DataSources.AsNoTracking()
            where s.Id == sourceId
            join e in db.Economies on s.EconomyCode equals e.Code
            select e.TimeZone).FirstOrDefaultAsync(ct);
        return new SourceContext(indicators, mappedKeys, ignoredKeys, sourceTimeZone ?? FallbackTimeZone);
    }

    private void Reject(IngestRequest request, ScheduleEntryRecord entry, string reason, Counts counts)
    {
        logger.LogWarning("运行记录 {RunId} 的日程条目 {ReleaseKey} {ReferencePeriod} 无法入库（ERR-PARSE-005）：{Reason}",
            request.RunId, entry.ReleaseKey, entry.ReferencePeriod, reason);
        counts.Failed++;
    }

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

    private sealed record SourceContext(
        Dictionary<string, List<ScheduleIndicator>> Indicators,
        HashSet<string> MappedKeys,
        HashSet<string> IgnoredKeys,
        string SourceTimeZone);

    private sealed class Counts
    {
        public int Inserted { get; set; }

        public int Updated { get; set; }

        public int Skipped { get; set; }

        public int Failed { get; set; }
    }
}
