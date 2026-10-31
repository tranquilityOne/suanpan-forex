using Microsoft.AspNetCore.Mvc;
using Suanpan.Crm.Models.Calendar;
using Suanpan.Services.Calendar.Conflicts;
using Suanpan.Services.Calendar.Conflicts.Models;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Calendar.Controllers;

/// <summary>
/// 锁定冲突（日历设计 §3.9、§9.5，FR-011）：自动流程遇到锁定的字段、采集值与锁定值不同时记下的冲突。全部角色可以查看，
/// 管理员、编辑选择保持锁定或解锁并采用采集值（采集工程师、只读不能处理，ERR-AUTH-002），都带事件的 row_version（ERR-EVT-009）
/// </summary>
[Area("Calendar")]
[PermMenu("锁定冲突", Icon = "lock-exclamation", Sort = 40)]
public sealed class ConflictController(ILockConflictService conflicts) : Controller
{
    private const string Module = "财经日历";

    [HttpGet]
    public async Task<IActionResult> Index(bool resolved = false, int page = 1, CancellationToken ct = default)
        => View(new ConflictListViewModel { Resolved = resolved, Result = await conflicts.QueryAsync(new LockConflictQuery(resolved, page), ct) });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "保持锁定")]
    public async Task<IActionResult> Keep(long id, int rowVersion, CancellationToken ct)
    {
        var result = await conflicts.KeepAsync(id, rowVersion, ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? "已保持锁定，之后再采集到同一个值不再提示" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "解锁并采用采集值")]
    public async Task<IActionResult> Adopt(long id, int rowVersion, CancellationToken ct)
    {
        var result = await conflicts.AdoptAsync(id, rowVersion, ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? "已解锁并采用采集值，之后的采集会照常更新这个字段" : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
