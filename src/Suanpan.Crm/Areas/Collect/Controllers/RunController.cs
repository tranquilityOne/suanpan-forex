using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Core.Collect.Enums;
using Suanpan.Crm.Models.Collect;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Collect.Tasks;
using Suanpan.Shared.Auth;

namespace Suanpan.Crm.Areas.Collect.Controllers;

/// <summary>
/// 运行记录（采集设计 §8.1，FR-002 验收第 3、5 条）：只能查看。列表整页刷新；
/// 运行详情在排队、运行中时每 2 秒局部刷新，直到结束（前端约定 §4.1"手动触发采集"）。
/// </summary>
[Area("Collect")]
[PermMenu("运行记录", Icon = "history", Sort = 30)]
public sealed class RunController(
    ICollectRunService runs,
    ICollectTaskService tasks,
    IDataSourceService sources,
    IOperatorTimeZone operatorTimeZone) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CollectRunFilter filter, int page = 1, CancellationToken ct = default)
        => View(new CollectRunListViewModel
        {
            Filter = filter,
            Result = await runs.QueryAsync(filter.ToQuery(operatorTimeZone.TimeZone, page), ct),
            TaskOptions = (await tasks.GetAllAsync(ct)).Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToList(),
            SourceOptions = (await sources.GetListAsync(ct)).Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList(),
            StatusOptions = CollectEnumOptions.Of<CollectRunStatus>(),
            TriggerOptions = CollectEnumOptions.Of<CollectRunTrigger>(),
        });

    /// <summary>HTMX 请求（自动刷新）只返回详情区块</summary>
    [HttpGet]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        if (await runs.GetAsync(id, ct) is not { } detail)
        {
            return NotFound();
        }

        return Request.IsHtmx() ? PartialView("_Detail", detail) : View(detail);
    }
}
