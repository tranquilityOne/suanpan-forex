using Microsoft.AspNetCore.Mvc;
using Suanpan.Crm.Models.Collect;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Collect.Watch;
using Suanpan.Shared.Auth;

namespace Suanpan.Crm.Areas.Collect.Controllers;

/// <summary>
/// 盯盘状态（采集设计 §5.7、§8.1，FR-003）：全部角色可以查看，只读。"正在盯盘"区块每 5 秒局部刷新（HTMX，前端约定 §4.1），
/// 标签页不可见时暂停；接下来 24 小时的计划和最近 24 小时结束的盯盘随整页刷新
/// </summary>
[Area("Collect")]
[PermMenu("盯盘状态", Icon = "radar", Sort = 50)]
public sealed class WatchController(IWatchPlanService watch, TimeProvider timeProvider) : Controller
{
    private static readonly TimeSpan Range = TimeSpan.FromHours(24);

    /// <summary>HTMX 请求（自动刷新）只返回"正在盯盘"区块</summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var active = await watch.GetActiveAsync(ct);
        if (Request.IsHtmx())
        {
            return PartialView("_Active", active);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var plan = await watch.GetPlanAsync(now, now + Range, ct);
        var recent = await watch.GetRecentAsync(now - Range, ct);
        return View(WatchStatusViewModel.Create(plan, active, recent));
    }
}
