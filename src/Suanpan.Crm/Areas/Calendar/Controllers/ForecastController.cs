using Microsoft.AspNetCore.Mvc;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Crm.Models.Calendar.Forecasts;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Forecasts;
using Suanpan.Services.Calendar.Forecasts.Models;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Calendar.Controllers;

/// <summary>
/// 预期值录入（日历设计 §3.8、§9.4，FR-010）：待录入清单全部角色可以查看，管理员、编辑在行内保存（HTMX 只刷新这一行，
/// 保存后定位下一条，BR-059）；事件详情中的表单也提交到这里。公布后只有管理员能更正，原因必填（BR-057）
/// </summary>
[Area("Calendar")]
[PermMenu("预期值录入", Icon = "target-arrow", Sort = 14)]
public sealed class ForecastController(IForecastService forecasts, ICalendarEventService events) : Controller
{
    private const string Module = "财经日历";

    [HttpGet]
    public async Task<IActionResult> Index(bool all = false, int page = 1, CancellationToken ct = default)
        => View(new ForecastListViewModel
        {
            All = all,
            Result = await forecasts.QueryPendingAsync(new ForecastQuery(all, page), ct),
            NoteOptions = await forecasts.GetNoteOptionsAsync(ct),
        });

    /// <summary>公布前录入、修改：清单中（HTMX）返回这一行；事件详情中（整页提交）回到详情</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "录入预期值")]
    public async Task<IActionResult> Save(long id, ForecastFormModel model, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await forecasts.SaveAsync(id, model.ToInput(), model.RowVersion, ct)
            : ManageResult.Fail(ForecastFormModel.FirstError(ModelState));
        if (!Request.IsHtmx())
        {
            TempData[result.Success ? "Message" : "Error"] = result.Success ? "预期值已保存并锁定" : result.Error;
            return RedirectToAction("Detail", "Event", new { id });
        }

        if (await forecasts.GetRowAsync(id, ct) is not { } row)
        {
            return NotFound();
        }

        return PartialView("_Row", result.Success ? ForecastRowViewModel.Done(row) : ForecastRowViewModel.Failed(row, model, result.Error!));
    }

    /// <summary>公布后更正（BR-057）：从事件详情进入，单独的表单页，提交前再确认一次</summary>
    [HttpGet]
    public async Task<IActionResult> Correct(long id, CancellationToken ct)
    {
        if (await events.GetDetailAsync(id, ct) is not { } detail)
        {
            return NotFound();
        }

        if (!ForecastRule.CanCorrect(detail.Event))
        {
            TempData["Error"] = "事件已取消，不能更正预期值";
            return RedirectToAction("Detail", "Event", new { id });
        }

        return View(ForecastCorrectViewModel.From(detail, await forecasts.GetNoteOptionsAsync(ct)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "更正预期值")]
    public async Task<IActionResult> Correct(long id, ForecastCorrectViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await forecasts.CorrectAsync(id, model.ToInput(), model.Reason, model.RowVersion, ct);
            if (result.Success)
            {
                TempData["Message"] = "预期值已更正";
                return RedirectToAction("Detail", "Event", new { id });
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        model.Detail = await events.GetDetailAsync(id, ct);
        model.NoteOptions = await forecasts.GetNoteOptionsAsync(ct);
        return model.Detail is null ? NotFound() : View(model);
    }
}
