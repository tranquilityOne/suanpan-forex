using Microsoft.AspNetCore.Mvc;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Crm.Models.Calendar;
using Suanpan.Services.Calendar.Events;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Calendar.Controllers;

/// <summary>
/// 待复核（日历设计 §9.3，BR-051）：质量校验未通过或官方更正的事件，由编辑确认采集值、改为人工值（事件的修改数值页）或驳回。
/// 驳回只适用于从未公布过的事件，原因必填；官方更正的事件只能采用新值或保留原值。管理员、编辑可以操作，其他角色只能查看
/// </summary>
[Area("Calendar")]
[PermMenu("待复核", Icon = "alert-triangle", Sort = 12)]
public sealed class ReviewController(ICalendarEventService events) : Controller
{
    private const string Module = "财经日历";

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
        => View(await events.QueryReviewAsync(page, ct: ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "确认采集值")]
    public async Task<IActionResult> Confirm(long id, long sourceId, int rowVersion, CancellationToken ct)
    {
        var result = await events.ConfirmReviewAsync(id, sourceId, rowVersion, ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? "已采用采集值，事件改为已公布" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>驳回要填原因，单独一页；提交前再确认一次</summary>
    [HttpGet]
    public async Task<IActionResult> Reject(long id, CancellationToken ct)
    {
        if (await events.GetDetailAsync(id, ct) is not { } detail || !EventEditRule.CanReject(detail.Event))
        {
            TempData["Error"] = "事件已不是从未公布过的待复核事件，不能驳回";
            return RedirectToAction(nameof(Index));
        }

        return View(new EventReasonViewModel { RowVersion = detail.Event.RowVersion, Detail = detail });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "驳回待复核")]
    public async Task<IActionResult> Reject(long id, EventReasonViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await events.RejectReviewAsync(id, model.Reason, model.RowVersion, ct);
            if (result.Success)
            {
                TempData["Message"] = "已驳回，事件改为延迟，继续补采";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        model.Detail = await events.GetDetailAsync(id, ct);
        return model.Detail is null ? RedirectToAction(nameof(Index)) : View(model);
    }
}
