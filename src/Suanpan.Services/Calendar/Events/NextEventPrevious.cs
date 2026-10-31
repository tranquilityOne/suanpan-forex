using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Data;

namespace Suanpan.Services.Calendar.Events;

/// <summary>
/// 公布值填为下一个未公布事件的前值（BR-044，与公布值入库相同，日历设计 §5.3）：人工录入公布值、确认采集值、
/// 解锁并采用采集值（§3.9）共用；下一期的前值被锁定时不改
/// </summary>
internal static class NextEventPrevious
{
    /// <summary>未公布的状态：下一期只取这些状态的事件（与公布值入库相同）</summary>
    private static readonly CalendarEventStatus[] UnreleasedStatuses =
        [CalendarEventStatus.Scheduled, CalendarEventStatus.Watching, CalendarEventStatus.Delayed, CalendarEventStatus.Unconfirmed];

    public static async Task FillAsync(
        SuanpanDbContext db, CalendarEvent calendarEvent, decimal value, decimal? upper, EventChangeActor actor, CancellationToken ct)
    {
        var next = await db.CalendarEvents
            .Where(e => e.IndicatorCode == calendarEvent.IndicatorCode && string.Compare(e.ReferencePeriod, calendarEvent.ReferencePeriod) > 0
                && UnreleasedStatuses.Contains(e.Status))
            .OrderBy(e => e.ReferencePeriod)
            .FirstOrDefaultAsync(ct);
        if (next is null || (next.Previous == value && next.PreviousUpper == upper) || EventLockRule.IsLocked(next.LockedFields, EventField.Previous))
        {
            return;
        }

        db.CalendarEventChanges.Add(EventChanges.Create(next.Id, EventChangeKind.Previous,
            EventChanges.Number(next.Previous), EventChanges.Number(value), actor, $"按 {calendarEvent.ReferencePeriod} 的公布值填入"));
        next.Previous = value;
        next.PreviousUpper = upper;
        next.RowVersion++;
    }
}
