using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Reference.Rules;
using Suanpan.Crm.Models.Calendar;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Reference.Economies;
using Suanpan.Shared.Auth;

namespace Suanpan.Crm.Areas.Calendar.Controllers;

/// <summary>
/// 日历事件（日历设计 §9.3，FR-009）：列表和详情，全部角色可以查看（PRD §13.1.2）。列表默认本周，时间按操作者时区显示（BR-048）；
/// 详情显示各字段的值、来源、锁定信息、各数据源的数值、锁定冲突和变更历史。人工修改、待复核的处理在 FR-009.2 加入
/// </summary>
[Area("Calendar")]
[PermMenu("日历事件", Icon = "calendar-event", Sort = 10)]
public sealed class EventController(
    ICalendarEventService events,
    IEconomyService economies,
    IOperatorTimeZone operatorTimeZone,
    TimeProvider timeProvider) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(EventFilter filter, int page = 1, CancellationToken ct = default)
    {
        var timeZone = operatorTimeZone.TimeZone;
        filter.DefaultToWeekOf(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime));
        return View(new EventListViewModel
        {
            Filter = filter,
            Result = await events.QueryAsync(filter.ToQuery(timeZone, page), ct),
            EconomyOptions = (await economies.GetListAsync(ct))
                .Select(e => new SelectListItem($"{e.Code} {e.Name.Get(ContentLanguage.Zh)}", e.Code))
                .ToList(),
            ImportanceOptions = Enum.GetValues<Importance>()
                .OrderByDescending(i => i)
                .Select(i => new SelectListItem(ReferenceDisplayNames.Get(i).Get(ContentLanguage.Zh), i.ToString()))
                .ToList(),
            StatusOptions = Enum.GetValues<CalendarEventStatus>()
                .Select(s => new SelectListItem(CalendarDisplayNames.Get(s), s.ToString()))
                .ToList(),
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
        => await events.GetDetailAsync(id, ct) is { } detail ? View(detail) : NotFound();
}
