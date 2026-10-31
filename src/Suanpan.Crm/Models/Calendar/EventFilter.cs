using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Reference.Enums;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Calendar.Events.Models;

namespace Suanpan.Crm.Models.Calendar;

/// <summary>
/// 事件列表的筛选条件（BR-048，日历设计 §9.3）。日期按操作者时区选，包含两端，按排期时间筛选；
/// 起止都没填时为本周（周一至周日）
/// </summary>
public sealed class EventFilter
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public string? EconomyCode { get; set; }

    public Importance? Importance { get; set; }

    public CalendarEventStatus? Status { get; set; }

    /// <summary>只看缺少预期值的</summary>
    public bool MissingForecast { get; set; }

    /// <summary>起止都没填时填上 today 所在的一周</summary>
    public void DefaultToWeekOf(DateOnly today)
    {
        if (From is null && To is null)
        {
            (From, To) = WeekOf(today);
        }
    }

    public EventQuery ToQuery(TimeZoneInfo timeZone, int page = 1)
    {
        var (begin, end) = DateRangeFilter.ToUtc(From, To, timeZone);
        return new EventQuery(begin, end, EconomyCode, Importance, Status, MissingForecast, page);
    }

    /// <summary>一周从周一开始（§12 第 17 项）</summary>
    public static (DateOnly Monday, DateOnly Sunday) WeekOf(DateOnly day)
    {
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        return (monday, monday.AddDays(6));
    }
}
