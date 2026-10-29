using Microsoft.AspNetCore.Mvc;
using Suanpan.Crm.Models.Calendar;
using Suanpan.Services.Calendar.Events;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Calendar.Controllers;

/// <summary>
/// 待确认事件（日历设计 §9.1，BR-040）：从官方日程中消失的事件由编辑保留或取消。管理员、编辑可以操作，
/// 采集工程师和只读只能查看（PRD §13.1.2）。保留、取消都带读取时的 row_version，被他人或采集改过时提示刷新（ERR-EVT-009）
/// </summary>
[Area("Calendar")]
[PermMenu("待确认事件", Icon = "calendar-question", Sort = 20)]
public sealed class UnconfirmedController(ICalendarEventService events) : Controller
{
    private const string Module = "财经日历";

    private const string GoneError = "事件已不是待确认，可能已被他人处理或重新出现在官方日程中";

    [HttpGet]
    public async Task<IActionResult> Index(UnconfirmedFilter filter, int page = 1, CancellationToken ct = default)
        => View(new UnconfirmedListViewModel { Filter = filter, Result = await events.QueryUnconfirmedAsync(filter.ToQuery(page), ct) });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "保留待确认事件")]
    public async Task<IActionResult> Keep(long id, int rowVersion, CancellationToken ct)
    {
        var result = await events.KeepAsync(id, rowVersion, ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? "事件已保留，改回待公布，以后不再随官方日程标为待确认" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>取消要填原因，单独一页；提交前再确认一次</summary>
    [HttpGet]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        if (await events.GetUnconfirmedAsync(id, ct) is not { } row)
        {
            TempData["Error"] = GoneError;
            return RedirectToAction(nameof(Index));
        }

        return View(new UnconfirmedCancelViewModel { RowVersion = row.RowVersion, Event = row });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "取消待确认事件")]
    public async Task<IActionResult> Cancel(long id, UnconfirmedCancelViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await events.CancelAsync(id, model.Reason, model.RowVersion, ct);
            if (result.Success)
            {
                TempData["Message"] = "事件已取消";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        model.Event = await events.GetUnconfirmedAsync(id, ct);
        if (model.Event is null)
        {
            TempData["Error"] = GoneError;
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }
}
