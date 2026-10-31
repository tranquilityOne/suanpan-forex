using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Records;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Ops.Enums;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Collect.Ingest;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Common.Alerts;
using Suanpan.Shared.Messages;
using Suanpan.Shared.Messages.Calendar;

namespace Suanpan.Services.Calendar.Releases;

/// <summary>
/// 公布值入库（FR-008，日历设计 §5）：公布值解析器输出的 <see cref="ReleaseValueRecord"/> 按"公布值"用途的映射找到指标，
/// 每个指标按 <see cref="ReleaseValueRule.Plan"/> 处理最新一期、前值和前值修正、更早各期的修正后数值。
/// 整个入库在一个事务中完成，同样的数据重复入库结果不变；每个指标的事件按 id 顺序行锁读取（§6.1）。
/// 消息（EV-004、EV-005）和告警在事务提交后发出，失败只记日志
/// </summary>
public sealed class CalendarReleaseIngestor(
    SuanpanDbContext db,
    IReleaseValidator validator,
    ISnapshotService snapshots,
    IAlertService alerts,
    IAuditActorProvider auditActor,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<CalendarReleaseIngestor> logger) : ICollectIngestor
{
    private const int MaxRawLength = 1000;

    private static readonly short[] UnreleasedStatuses =
    [
        (short)CalendarEventStatus.Scheduled, (short)CalendarEventStatus.Watching,
        (short)CalendarEventStatus.Delayed, (short)CalendarEventStatus.Unconfirmed,
    ];

    public CollectTargetType TargetType => CollectTargetType.Release;

    public async Task<IngestResult> IngestAsync(IngestRequest request, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var counts = new Counts();
        var groups = await GroupAsync(request, counts, ct);
        var outbox = new Outbox();
        try
        {
            await db.InTransactionAsync(async () =>
            {
                foreach (var group in groups.OrderBy(g => g.Indicator.Code, StringComparer.Ordinal))
                {
                    await IngestIndicatorAsync(request, group, now, counts, outbox, ct);
                }

                if (outbox.ReferencedSnapshots.Count > 0)
                {
                    await snapshots.MarkReferencedAsync(outbox.ReferencedSnapshots, ct);
                }
            }, ct);
        }
        finally
        {
            // 执行器在同一个 DbContext 中继续改运行记录：入库失败回滚后不能留下跟踪中的改动
            db.ChangeTracker.Clear();
        }

        await FlushAsync(outbox, ct);
        return new IngestResult(counts.Inserted, counts.Updated, counts.Skipped, counts.Failed, counts.Locked);
    }

    /// <summary>
    /// 校验并按指标分组（§5.2 第 1、2 步）：没有映射的外部标识、停用的指标计为跳过；参考期写法不对（ERR-PARSE-005）、
    /// 单位与指标不同（ERR-PARSE-006）、区间型指标缺上限的计为失败；值按指标小数位四舍五入（BR-043）
    /// </summary>
    private async Task<List<IndicatorGroup>> GroupAsync(IngestRequest request, Counts counts, CancellationToken ct)
    {
        var mappings = (await (
            from m in db.IndicatorSources.AsNoTracking()
            where m.SourceId == request.SourceId && m.Purpose == IndicatorSourcePurpose.Release
            join i in db.Indicators on m.IndicatorCode equals i.Code
            join e in db.Economies on i.EconomyCode equals e.Code
            select new { m.ExternalId, m.Priority, Indicator = i, e.TimeZone }).ToListAsync(ct))
            .ToDictionary(m => m.ExternalId, StringComparer.Ordinal);

        var groups = new Dictionary<string, IndicatorGroup>(StringComparer.Ordinal);
        foreach (var record in request.Records)
        {
            if (record is not ReleaseValueRecord value)
            {
                logger.LogError("运行记录 {RunId} 的记录类型 {Type} 不是公布值，解析器与目标数据类型不符", request.RunId, record.GetType().Name);
                counts.Failed++;
                continue;
            }

            if (!mappings.TryGetValue(value.ExternalId, out var mapping) || !mapping.Indicator.IsEnabled)
            {
                logger.LogWarning("运行记录 {RunId} 的外部标识 {ExternalId} 没有启用指标的公布值映射，跳过（任务参数与映射不一致）",
                    request.RunId, value.ExternalId);
                counts.Skipped++;
                continue;
            }

            var indicator = mapping.Indicator;
            var invalid = ReferencePeriodRule.Validate(value.ReferencePeriod, indicator.Frequency) is { } periodError
                ? $"{periodError}（ERR-PARSE-005）"
                : value.Unit != indicator.Unit
                    ? $"单位与指标不一致（ERR-PARSE-006）"
                    : indicator.ValueType == IndicatorValueType.Range && value.ValueUpper is null
                        ? "区间型指标缺少上限"
                        : null;
            if (invalid is not null)
            {
                logger.LogWarning("运行记录 {RunId} 的 {ExternalId} {ReferencePeriod} 无法入库：{Reason}",
                    request.RunId, value.ExternalId, value.ReferencePeriod, invalid);
                counts.Failed++;
                continue;
            }

            var upper = indicator.ValueType == IndicatorValueType.Range ? ReleaseValueRule.Round(value.ValueUpper!.Value, indicator.Decimals) : (decimal?)null;
            var point = new ReleasePoint(value.ReferencePeriod, ReleaseValueRule.Round(value.Value, indicator.Decimals), upper);
            if (!groups.TryGetValue(indicator.Code, out var group))
            {
                group = new IndicatorGroup(indicator, mapping.Priority, mapping.TimeZone);
                groups[indicator.Code] = group;
            }

            group.Points[point.Period] = (point, value);
        }

        return groups.Values.ToList();
    }

    private async Task IngestIndicatorAsync(IngestRequest request, IndicatorGroup group, DateTime now, Counts counts, Outbox outbox, CancellationToken ct)
    {
        var code = group.Indicator.Code;
        var periods = group.Points.Keys.ToArray();
        var latestPeriod = periods.Max(StringComparer.Ordinal)!;

        // 行锁：数据中各期的事件，以及最新一期之后的下一个未公布事件（填前值用）
        var ids = await db.Database.SqlQuery<long>($"""
            SELECT id AS "Value" FROM cal_event
            WHERE indicator_code = {code} AND (reference_period = ANY({periods}) OR id = (
                SELECT n.id FROM cal_event n
                WHERE n.indicator_code = {code} AND n.reference_period > {latestPeriod} AND n.status = ANY({UnreleasedStatuses})
                ORDER BY n.reference_period LIMIT 1))
            ORDER BY id FOR UPDATE
            """).ToListAsync(ct);
        var events = await db.CalendarEvents.Where(e => ids.Contains(e.Id)).ToListAsync(ct);
        var byPeriod = events.Where(e => periods.Contains(e.ReferencePeriod)).ToDictionary(e => e.ReferencePeriod, StringComparer.Ordinal);
        var next = events.Where(e => string.CompareOrdinal(e.ReferencePeriod, latestPeriod) > 0).OrderBy(e => e.ReferencePeriod, StringComparer.Ordinal).FirstOrDefault();
        var hasReleased = await db.CalendarEvents.AnyAsync(
            e => e.IndicatorCode == code && (e.Status == CalendarEventStatus.Released || e.Actual != null), ct);

        var plan = ReleaseValueRule.Plan(group.Points.Values.Select(p => p.Point).ToList(), byPeriod, hasReleased);
        var context = new LatestContext(request, group, plan.Latest, group.Points[plan.Latest.Period].Record, now,
            new EventChangeActor(ChangeActorKind.Collector, auditActor.GetActor(), now, request.SourceId, request.RunId));
        var latestEvent = byPeriod.GetValueOrDefault(plan.Latest.Period);

        switch (plan.Action)
        {
            case LatestAction.InitialPrevious:
                // 刚开始采集（BR-157）：不补建，最新一期的值填为下一个事件的前值
                if (next is not null)
                {
                    await SetPreviousAsync(next, plan.Latest.Value, plan.Latest.Upper, context, outbox, ct);
                }

                counts.Skipped++;
                break;
            case LatestAction.Supplement:
                latestEvent = await SupplementAsync(context, ct);
                outbox.Alert(AlertLevel.Important, code, $"{code} {plan.Latest.Period} 找不到排期，已按检测时间补建事件（ERR-EVT-006）");
                counts.Inserted++;
                await ReleaseAsync(latestEvent, next, context, outbox, ct);
                break;
            case LatestAction.Release:
                counts.Updated++;
                await ReleaseAsync(latestEvent!, next, context, outbox, ct);
                break;
            case LatestAction.Correction:
                // 仍是最新一期却变了：官方更正，原公布值不变，进入待复核（BR-046，ERR-EVT-007）
                counts.Updated++;
                await UpsertValueAsync(latestEvent!, context, outbox, ct);
                HoldForReview(latestEvent!, "官方更正：已公布的值再次采集到不同数值（ERR-EVT-007）", context, outbox);
                break;
            default:
                await UpsertValueAsync(latestEvent!, context, outbox, ct);
                if (plan.ActualLockConflict)
                {
                    await RecordConflictAsync(
                        latestEvent!, EventField.Actual, (latestEvent!.Actual, latestEvent.ActualUpper), (plan.Latest.Value, plan.Latest.Upper), context, ct);
                    counts.Locked++;
                }
                else
                {
                    counts.Skipped++;
                }

                if (latestEvent!.Status == CalendarEventStatus.Cancelled)
                {
                    logger.LogWarning("事件 {IndicatorCode} {ReferencePeriod} 已取消，采集到的值只记在各数据源的数值中", code, plan.Latest.Period);
                }

                break;
        }

        if (latestEvent is not null && plan.Previous is { } previous)
        {
            await SetPreviousAsync(latestEvent, previous.Previous, previous.PreviousUpper, context, outbox, ct);
            await SetPreviousRevisedAsync(latestEvent, previous.PreviousRevised, context, ct);
        }

        // 更早各期的修正后数值（BR-044）：不可锁定，直接更新
        foreach (var revision in plan.Revisions)
        {
            db.CalendarEventChanges.Add(EventChanges.Create(revision.Event.Id, EventChangeKind.RevisedValue,
                EventChanges.Number(revision.Event.RevisedValue), EventChanges.Number(revision.RevisedValue), context.Actor));
            revision.Event.RevisedValue = revision.RevisedValue;
            revision.Event.RowVersion++;
        }

        counts.Updated += plan.Revisions.Count;
        counts.Skipped += group.Points.Count - 1 - plan.Revisions.Count;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 未公布的事件拿到值（§5.2）：先记各数据源的数值，再做质量校验（§5.6）。通过则写入公布值、改为"已公布"（BR-045），
    /// 并把公布值填为下一期的前值；不通过则进入"待复核"，公布值不写入事件
    /// </summary>
    private async Task ReleaseAsync(CalendarEvent calendarEvent, CalendarEvent? next, LatestContext context, Outbox outbox, CancellationToken ct)
    {
        await UpsertValueAsync(calendarEvent, context, outbox, ct);
        var others = await db.CalendarEventValues.AsNoTracking()
            .Where(v => v.EventId == calendarEvent.Id && v.SourceId != context.Request.SourceId)
            .ToListAsync(ct);
        var point = context.Point;
        var validation = await validator.ValidateAsync(
            new ReleaseCandidate(context.Group.Indicator, calendarEvent, point.Value, point.Upper, context.Request.SourceId, context.Group.Priority, others), ct);
        if (!validation.Passed)
        {
            HoldForReview(calendarEvent, $"校验未通过：{string.Join("、", validation.FailedRules)}（ERR-EVT-002）", context, outbox);
            return;
        }

        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Actual,
            EventChanges.Number(calendarEvent.Actual), EventChanges.Number(point.Value), context.Actor));
        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Status,
            EventChanges.Status(calendarEvent.Status), EventChanges.Status(CalendarEventStatus.Released), context.Actor));
        calendarEvent.Actual = point.Value;
        calendarEvent.ActualUpper = point.Upper;
        calendarEvent.ActualRaw = Truncate(context.Record.Raw, MaxRawLength);
        calendarEvent.ActualSourceId = context.Request.SourceId;
        calendarEvent.DetectedAt = context.Now;
        calendarEvent.SourceSnapshotId = context.SnapshotId;
        calendarEvent.Status = CalendarEventStatus.Released;
        calendarEvent.RowVersion++;
        outbox.Reference(context.SnapshotId);
        outbox.Message(new EventReleasedEvent(
            calendarEvent.Id, calendarEvent.IndicatorCode, calendarEvent.ReferencePeriod, calendarEvent.ReleaseBatchId, context.Now));

        if (next is not null)
        {
            await SetPreviousAsync(next, point.Value, point.Upper, context, outbox, ct);
        }
    }

    /// <summary>进入"待复核"（EV-005）：告警级别按 BR-129，高重要性为紧急，其他为重要</summary>
    private void HoldForReview(CalendarEvent calendarEvent, string reason, LatestContext context, Outbox outbox)
    {
        if (calendarEvent.Status != CalendarEventStatus.PendingReview)
        {
            db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Status,
                EventChanges.Status(calendarEvent.Status), EventChanges.Status(CalendarEventStatus.PendingReview), context.Actor, reason));
            calendarEvent.Status = CalendarEventStatus.PendingReview;
            calendarEvent.RowVersion++;
        }

        outbox.Message(new EventHeldForReviewEvent(calendarEvent.Id, calendarEvent.IndicatorCode, reason));
        var level = context.Group.Indicator.Importance == Importance.High ? AlertLevel.Urgent : AlertLevel.Important;
        outbox.Alert(level, calendarEvent.IndicatorCode, $"{calendarEvent.IndicatorCode} {calendarEvent.ReferencePeriod} 进入待复核：{reason}");
    }

    /// <summary>补建（ERR-EVT-006）：来源"补建"，排期时间 = 检测时间，精度"精确到分钟"，没有维护排期的数据源</summary>
    private async Task<CalendarEvent> SupplementAsync(LatestContext context, CancellationToken ct)
    {
        var created = new CalendarEvent
        {
            IndicatorCode = context.Group.Indicator.Code,
            ReferencePeriod = context.Point.Period,
            Status = CalendarEventStatus.Scheduled,
            Origin = EventOrigin.Supplemented,
            TimeZone = context.Group.TimeZone,
            ScheduledAt = context.Now,
            ScheduledLocal = LocalTimeRule.ToLocal(context.Now, context.Group.TimeZone),
            TimePrecision = TimePrecision.Minute,
        };
        db.CalendarEvents.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>这个数据源的数值（cal_event_value）：每个事件每个数据源一行，值变了才更新，检测时间为第一次得到当前值的时间</summary>
    private async Task UpsertValueAsync(CalendarEvent calendarEvent, LatestContext context, Outbox outbox, CancellationToken ct)
    {
        var point = context.Point;
        var value = await db.CalendarEventValues.FirstOrDefaultAsync(v => v.EventId == calendarEvent.Id && v.SourceId == context.Request.SourceId, ct);
        if (value is not null && value.Value == point.Value && value.ValueUpper == point.Upper)
        {
            return;
        }

        if (value is null)
        {
            value = new CalendarEventValue { EventId = calendarEvent.Id, SourceId = context.Request.SourceId };
            db.CalendarEventValues.Add(value);
        }

        value.Value = point.Value;
        value.ValueUpper = point.Upper;
        value.Raw = Truncate(context.Record.Raw, MaxRawLength);
        value.SnapshotId = context.SnapshotId;
        value.RunId = context.Request.RunId;
        value.DetectedAt = context.Now;
        outbox.Reference(context.SnapshotId);
    }

    /// <summary>前值（连同上限，BR-044）；被锁定时不覆盖，值不同时写锁定冲突（BR-062）</summary>
    private async Task SetPreviousAsync(CalendarEvent calendarEvent, decimal value, decimal? upper, LatestContext context, Outbox outbox, CancellationToken ct)
    {
        if (calendarEvent.Previous == value && calendarEvent.PreviousUpper == upper)
        {
            return;
        }

        if (EventLockRule.IsLocked(calendarEvent.LockedFields, EventField.Previous))
        {
            await RecordConflictAsync(calendarEvent, EventField.Previous, (calendarEvent.Previous, calendarEvent.PreviousUpper), (value, upper), context, ct);
            return;
        }

        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Previous,
            EventChanges.Number(calendarEvent.Previous), EventChanges.Number(value), context.Actor));
        calendarEvent.Previous = value;
        calendarEvent.PreviousUpper = upper;
        calendarEvent.RowVersion++;
    }

    /// <summary>前值修正：本次发布对上一期的修正（BR-044）；被锁定时不覆盖，值不同时写锁定冲突</summary>
    private async Task SetPreviousRevisedAsync(CalendarEvent calendarEvent, decimal? value, LatestContext context, CancellationToken ct)
    {
        if (calendarEvent.PreviousRevised == value)
        {
            return;
        }

        if (EventLockRule.IsLocked(calendarEvent.LockedFields, EventField.PreviousRevised))
        {
            await RecordConflictAsync(calendarEvent, EventField.PreviousRevised, (calendarEvent.PreviousRevised, null), (value, null), context, ct);
            return;
        }

        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.PreviousRevised,
            EventChanges.Number(calendarEvent.PreviousRevised), EventChanges.Number(value), context.Actor));
        calendarEvent.PreviousRevised = value;
        calendarEvent.RowVersion++;
    }

    /// <summary>
    /// 锁定冲突（BR-062）：同一事件同一字段只有一条待处理的冲突，再次冲突时更新采集值和最近时间；
    /// 没有待处理的冲突、而这个采集值在当前这把锁下已被保持锁定过时，不再产生冲突（日历设计 §3.9）
    /// </summary>
    private async Task RecordConflictAsync(
        CalendarEvent calendarEvent, EventField field, (decimal? Value, decimal? Upper) locked, (decimal? Value, decimal? Upper) collected,
        LatestContext context, CancellationToken ct)
    {
        var collectedValue = EventChanges.Number(collected.Value) ?? "null";
        var conflicts = await db.LockConflicts.Where(c => c.EventId == calendarEvent.Id && c.Field == field).ToListAsync(ct);
        var conflict = conflicts.FirstOrDefault(c => c.Status == LockConflictStatus.Pending);
        if (conflict is null)
        {
            var fieldLock = calendarEvent.LockedFields.First(l => l.Field == field);
            if (LockConflictRule.WasKept(conflicts, fieldLock, collectedValue, collected.Upper))
            {
                return;
            }

            conflict = new LockConflict { EventId = calendarEvent.Id, Field = field, FirstDetectedAt = context.Now };
            db.LockConflicts.Add(conflict);
        }

        conflict.LockedValue = EventChanges.Number(locked.Value) ?? "null";
        conflict.LockedUpper = locked.Upper;
        conflict.CollectedValue = collectedValue;
        conflict.CollectedUpper = collected.Upper;
        conflict.SourceId = context.Request.SourceId;
        conflict.SnapshotId = context.SnapshotId;
        conflict.RunId = context.Request.RunId;
        conflict.LastDetectedAt = context.Now;
    }

    private async Task FlushAsync(Outbox outbox, CancellationToken ct)
    {
        foreach (var message in outbox.Messages)
        {
            try
            {
                await eventBus.PublishAsync(message, ct: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "消息 {MessageName} 发布失败", message.MessageName);
            }
        }

        foreach (var (level, subject, message) in outbox.Alerts)
        {
            try
            {
                await alerts.RaiseAsync(level, subject, message, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "告警发送失败：{Subject} {Message}", subject, message);
            }
        }
    }

    private static string Truncate(string value, int maxLength) => value.Length > maxLength ? value[..maxLength] : value;

    /// <summary>一个指标在这次数据中的各期（参考期 → 舍入后的值和原始记录）、公布值映射的优先级、所属经济体的时区</summary>
    private sealed record IndicatorGroup(Indicator Indicator, short? Priority, string TimeZone)
    {
        public Dictionary<string, (ReleasePoint Point, ReleaseValueRecord Record)> Points { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>处理最新一期时用到的上下文</summary>
    private sealed record LatestContext(
        IngestRequest Request, IndicatorGroup Group, ReleasePoint Point, ReleaseValueRecord Record, DateTime Now, EventChangeActor Actor)
    {
        /// <summary>最新一期来自的响应对应的快照</summary>
        public long? SnapshotId => Record.ResponseIndex >= 0 && Record.ResponseIndex < Request.SnapshotIds.Count
            ? Request.SnapshotIds[Record.ResponseIndex]
            : null;
    }

    /// <summary>事务提交后才发出的消息和告警，以及要标记为被引用的快照</summary>
    private sealed class Outbox
    {
        public List<IntegrationMessage> Messages { get; } = [];

        public List<(AlertLevel Level, string Subject, string Message)> Alerts { get; } = [];

        public HashSet<long> ReferencedSnapshots { get; } = [];

        public void Message(IntegrationMessage message) => Messages.Add(message);

        public void Alert(AlertLevel level, string subject, string message) => Alerts.Add((level, subject, message));

        public void Reference(long? snapshotId)
        {
            if (snapshotId is { } id)
            {
                ReferencedSnapshots.Add(id);
            }
        }
    }

    private sealed class Counts
    {
        public int Inserted { get; set; }

        public int Updated { get; set; }

        public int Skipped { get; set; }

        public int Failed { get; set; }

        public int Locked { get; set; }
    }
}
