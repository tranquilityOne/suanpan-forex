using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Services.Calendar.Forecasts.Models;
using Suanpan.Services.Common.Querying;
using Suanpan.Services.Sys.Configs;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Calendar.Forecasts;

public sealed class ForecastService(
    SuanpanDbContext db,
    IAuditActorProvider auditActor,
    IConfigService config,
    TimeProvider timeProvider) : IForecastService
{
    private const string StaleError = "事件已被修改，请刷新页面后重试";

    /// <summary>公布前录入时锁定的原因（日历设计 §3.8）</summary>
    private const string EnterReason = "录入预期值";

    /// <summary>未公布的状态（BR-055）：待公布、盯盘中、延迟、待确认</summary>
    private static readonly CalendarEventStatus[] UnreleasedStatuses =
        [CalendarEventStatus.Scheduled, CalendarEventStatus.Watching, CalendarEventStatus.Delayed, CalendarEventStatus.Unconfirmed];

    public Task<PagedResult<EventRow>> QueryPendingAsync(ForecastQuery query, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var end = now.AddDays(ForecastRule.PendingDays);
        var events = db.CalendarEvents.AsNoTracking()
            .Where(e => UnreleasedStatuses.Contains(e.Status) && e.Actual == null && e.Forecast == null
                && e.ScheduledAt >= now && e.ScheduledAt < end);
        var indicators = db.Indicators.AsNoTracking();
        if (!query.AllImportance)
        {
            indicators = indicators.Where(i => i.Importance == Importance.High);
        }

        return (from e in events
                join i in indicators on e.IndicatorCode equals i.Code
                orderby e.ScheduledAt, e.Id
                select new EventRow(e, i.ShortName, i.EconomyCode, i.Importance, i.ValueType, i.Unit, i.Decimals))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
    }

    public Task<EventRow?> GetRowAsync(long id, CancellationToken ct = default)
        => (from e in db.CalendarEvents.AsNoTracking()
            where e.Id == id
            join i in db.Indicators on e.IndicatorCode equals i.Code
            select new EventRow(e, i.ShortName, i.EconomyCode, i.Importance, i.ValueType, i.Unit, i.Decimals))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<string>> GetNoteOptionsAsync(CancellationToken ct = default)
    {
        var value = await config.GetStringAsync(CalendarConfigKeys.ForecastNoteOptions, string.Empty, ct);

        // 中文逗号也认，运营人员在配置页面里常这样写
        return (value ?? string.Empty)
            .Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();
    }

    public async Task<ManageResult> SaveAsync(long id, ForecastInput input, int rowVersion, CancellationToken ct = default)
    {
        var note = input.Note?.Trim();
        if (CheckNote(note) is { } noteError)
        {
            return noteError;
        }

        var (calendarEvent, error) = await FindAsync(id, rowVersion, ct);
        if (calendarEvent is null)
        {
            return error!;
        }

        if (!ForecastRule.CanEnter(calendarEvent))
        {
            return ForecastRule.IsReleased(calendarEvent) ? ManageResult.Fail("事件已公布") : ManageResult.Fail("事件已取消，不能录入预期值");
        }

        return await ApplyAsync(calendarEvent, input, note, EnterReason, requireChange: false, ct);
    }

    public async Task<ManageResult> CorrectAsync(long id, ForecastInput input, string? reason, int rowVersion, CancellationToken ct = default)
    {
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return ManageResult.Fail("请填写更正原因", "Reason");
        }

        if (reason.Length > ICalendarEventService.MaxReasonLength)
        {
            return ManageResult.Fail($"更正原因不能超过 {ICalendarEventService.MaxReasonLength} 个字", "Reason");
        }

        var note = input.Note?.Trim();
        if (CheckNote(note) is { } noteError)
        {
            return noteError;
        }

        var (calendarEvent, error) = await FindAsync(id, rowVersion, ct);
        if (calendarEvent is null)
        {
            return error!;
        }

        if (!ForecastRule.CanCorrect(calendarEvent))
        {
            return ManageResult.Fail("事件已取消，不能更正预期值");
        }

        return await ApplyAsync(calendarEvent, input, note, reason, requireChange: true, ct);
    }

    /// <summary>来源说明选填，≤ 100 字（BR-054）；note 已去掉首尾空白</summary>
    private static ManageResult? CheckNote(string? note)
        => note is { Length: > ForecastRule.MaxNoteLength }
            ? ManageResult.Fail($"来源说明不能超过 {ForecastRule.MaxNoteLength} 个字", nameof(ForecastInput.Note))
            : null;

    private static string? NoteOf(string? note) => string.IsNullOrEmpty(note) ? null : note;

    /// <summary>
    /// 写预期值、来源类型"人工录入"和来源说明，锁定预期值（BR-061），预期值变了时写一条变更历史；
    /// 格式按指标（BR-058，ERR-FC-001）；预期值和来源说明都没变时不改，更正（requireChange）时提示没有修改
    /// </summary>
    private async Task<ManageResult> ApplyAsync(
        CalendarEvent calendarEvent, ForecastInput input, string? note, string reason, bool requireChange, CancellationToken ct)
    {
        if (input.Forecast is not { } forecast)
        {
            return ManageResult.Fail("请填写预期值", nameof(ForecastInput.Forecast));
        }

        var indicator = await db.Indicators.AsNoTracking().FirstAsync(i => i.Code == calendarEvent.IndicatorCode, ct);
        var upper = indicator.ValueType == IndicatorValueType.Range ? input.ForecastUpper : null;
        if (EventValueRule.Validate(forecast, upper, indicator.ValueType, indicator.Decimals) is { } message)
        {
            return ManageResult.Fail(message, nameof(ForecastInput.Forecast));
        }

        note = NoteOf(note);
        var valueChanged = forecast != calendarEvent.Forecast || upper != calendarEvent.ForecastUpper;
        if (!valueChanged && note == calendarEvent.ForecastNote)
        {
            return requireChange ? ManageResult.Fail("没有修改预期值") : ManageResult.Ok;
        }

        var actor = new EventChangeActor(ChangeActorKind.User, auditActor.GetActor(), timeProvider.GetUtcNow().UtcDateTime);
        if (valueChanged)
        {
            db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Forecast,
                EventChanges.Number(calendarEvent.Forecast), EventChanges.Number(forecast), actor, reason));
        }

        calendarEvent.Forecast = forecast;
        calendarEvent.ForecastUpper = upper;
        calendarEvent.ForecastType = ForecastType.Manual;
        calendarEvent.ForecastNote = note;

        // 赋一个新的列表，jsonb 列的修改才会被检测到
        var locks = calendarEvent.LockedFields.ToList();
        EventLockRule.Lock(locks, EventField.Forecast, actor.ChangedBy, actor.ChangedAt, reason);
        calendarEvent.LockedFields = locks;
        calendarEvent.RowVersion++;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ManageResult.Fail(StaleError);
        }

        return ManageResult.Ok;
    }

    /// <summary>读出要改的事件；不存在，或 row_version 与读取时不同（他人已录入，ERR-FC-003）时返回错误</summary>
    private async Task<(CalendarEvent? Event, ManageResult? Error)> FindAsync(long id, int rowVersion, CancellationToken ct)
    {
        var calendarEvent = await db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (calendarEvent is null)
        {
            return (null, ManageResult.Fail("事件不存在"));
        }

        return calendarEvent.RowVersion == rowVersion ? (calendarEvent, null) : (null, ManageResult.Fail(StaleError));
    }
}
