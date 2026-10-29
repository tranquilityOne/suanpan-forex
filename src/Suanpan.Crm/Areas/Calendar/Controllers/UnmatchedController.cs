using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Crm.Models.Calendar;
using Suanpan.Services.Calendar.Unmatched;
using Suanpan.Services.Collect.Sources;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Calendar.Controllers;

/// <summary>
/// 未匹配条目（日历设计 §9.1，ERR-EVT-003、BR-156）：官方日程中没有排期映射的条目，按发布标识分组。
/// 编辑新增映射后重新匹配，或按发布标识忽略、恢复；管理员、编辑可以操作，采集工程师和只读只能查看（PRD §13.1.2）
/// </summary>
[Area("Calendar")]
[PermMenu("未匹配条目", Icon = "link-off", Sort = 30)]
public sealed class UnmatchedController(IUnmatchedEntryService unmatched, IDataSourceService sources) : Controller
{
    private const string Module = "财经日历";

    /// <summary>?view=ignored 显示已忽略的发布</summary>
    [HttpGet]
    public async Task<IActionResult> Index(UnmatchedFilter filter, int page = 1, CancellationToken ct = default)
    {
        var sourceOptions = (await sources.GetListAsync(ct)).Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList();
        if (filter.ShowIgnored)
        {
            return View("Ignored", new IgnoredReleaseListViewModel
            {
                Filter = filter,
                Result = await unmatched.QueryIgnoredAsync(filter.ToIgnoredQuery(page), ct),
                SourceOptions = sourceOptions,
            });
        }

        return View(new UnmatchedListViewModel
        {
            Filter = filter,
            Result = await unmatched.QueryAsync(filter.ToQuery(page), ct),
            SourceOptions = sourceOptions,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "忽略发布")]
    public async Task<IActionResult> Ignore(long sourceId, string releaseKey, long? returnSourceId, CancellationToken ct)
    {
        var result = await unmatched.IgnoreAsync(sourceId, releaseKey, ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? $"已忽略发布 {releaseKey}，以后这个发布的条目不再列出" : result.Error;
        return RedirectToAction(nameof(Index), new { SourceId = returnSourceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "恢复已忽略的发布")]
    public async Task<IActionResult> Restore(long id, long? returnSourceId, CancellationToken ct)
    {
        var result = await unmatched.RestoreAsync(id, ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? "已恢复，下一次同步日程时这个发布的条目会重新出现" : result.Error;
        return RedirectToAction(nameof(Index), new { SourceId = returnSourceId, View = UnmatchedFilter.IgnoredView });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "重新匹配未匹配条目")]
    public async Task<IActionResult> Rematch(long? returnSourceId, CancellationToken ct)
    {
        var matched = await unmatched.RematchAsync(ct);
        TempData["Message"] = matched > 0
            ? $"重新匹配完成：{matched} 条匹配上指标，已建成或更新日历事件"
            : "没有匹配上的条目：请先为发布标识新增排期映射";
        return RedirectToAction(nameof(Index), new { SourceId = returnSourceId });
    }
}
