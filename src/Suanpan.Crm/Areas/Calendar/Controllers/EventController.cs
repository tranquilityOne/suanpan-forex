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
using Suanpan.Services.Calendar.Forecasts;
using Suanpan.Services.Reference.Economies;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Calendar.Controllers;

/// <summary>
/// 日历事件（日历设计 §9.3，FR-009）：列表和详情，全部角色可以查看（PRD §13.1.2）。列表默认本周，时间按操作者时区显示（BR-048）；
/// 详情显示各字段的值、来源、锁定信息、各数据源的数值、锁定冲突和变更历史。修改数值、取消、解锁只给管理员、编辑（§3.7），
/// 都带读取时的 row_version，被他人或采集改过时提示刷新（ERR-EVT-009）
/// </summary>
[Area("Calendar")]
[PermMenu("日历事件", Icon = "calendar-event", Sort = 10)]
public sealed class EventController(
    ICalendarEventService events,
    IForecastService forecasts,
    IEconomyService economies,
    IOperatorTimeZone operatorTimeZone,
    TimeProvider timeProvider) : Controller
{
    private const string Module = "财经日历";

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

    /// <summary>公布前的事件带预期值表单（日历设计 §9.4），来源说明的常用选项放在 ViewData["ForecastNoteOptions"]</summary>
    [HttpGet]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        if (await events.GetDetailAsync(id, ct) is not { } detail)
        {
            return NotFound();
        }

        if (ForecastRule.CanEnter(detail.Event))
        {
            ViewData["ForecastNoteOptions"] = await forecasts.GetNoteOptionsAsync(ct);
        }

        return View(detail);
    }

    /// <summary>修改数值（日历设计 §3.7）：单独的表单页，原因必填，提交前再确认一次</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
        => await events.GetDetailAsync(id, ct) is { } detail ? View(EventEditViewModel.From(detail)) : NotFound();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "修改事件数值")]
    public async Task<IActionResult> Edit(long id, EventEditViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await events.EditValuesAsync(id, model.ToInput(), model.Reason, model.RowVersion, ct);
            if (result.Success)
            {
                TempData["Message"] = "已保存，改了的字段已锁定，之后的采集不会覆盖";
                return RedirectToAction(nameof(Detail), new { id });
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        model.Detail = await events.GetDetailAsync(id, ct);
        return model.Detail is null ? NotFound() : View(model);
    }

    /// <summary>取消（BR-049）：原因必填，单独的表单页</summary>
    [HttpGet]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
        => await events.GetDetailAsync(id, ct) is { } detail
            ? View(new EventReasonViewModel { RowVersion = detail.Event.RowVersion, Detail = detail })
            : NotFound();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "取消事件")]
    public async Task<IActionResult> Cancel(long id, EventReasonViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await events.CancelAsync(id, model.Reason, model.RowVersion, ct);
            if (result.Success)
            {
                TempData["Message"] = "事件已取消";
                return RedirectToAction(nameof(Detail), new { id });
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        model.Detail = await events.GetDetailAsync(id, ct);
        return model.Detail is null ? NotFound() : View(model);
    }

    /// <summary>解锁一个字段（BR-063）：下一次采集照常更新</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "解锁事件字段")]
    public async Task<IActionResult> Unlock(long id, EventField field, int rowVersion, CancellationToken ct)
    {
        var result = await events.UnlockAsync(id, field, rowVersion, ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? $"{CalendarDisplayNames.Get(field)}已解锁，下一次采集会照常更新" : result.Error;
        return RedirectToAction(nameof(Detail), new { id });
    }
}
