using Microsoft.AspNetCore.Mvc;
using Suanpan.Crm.Auth;
using Suanpan.Crm.Models.Sys;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Sys.Controllers;

/// <summary>
/// 权限点（sys 设计 §6.3、§9.1）：权限点由"从代码重建"生成，代码是唯一的事实来源。
/// 页面上只能改图标、排序、启停，不提供新增和删除；这些修改只到下次重建为止，重建时恢复为代码中的设置。
/// </summary>
[Area("Sys")]
[PermMenu("权限点", Icon = "key", Sort = 30)]
public sealed class PermissionController(IPermissionManageService permissions, IPermissionRebuildService rebuildService) : Controller
{
    private const string Module = "系统管理";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await permissions.GetTreeAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        if (await permissions.GetAsync(id, ct) is not { } permission)
        {
            return NotFound();
        }

        return View(new PermissionEditViewModel
        {
            Id = permission.Id,
            PermCode = permission.PermCode,
            PermName = permission.PermName,
            Icon = permission.Icon,
            Sort = permission.Sort,
            IsEnabled = permission.IsEnabled,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "修改权限点")]
    public async Task<IActionResult> Edit(PermissionEditViewModel model, CancellationToken ct)
    {
        if (await permissions.GetAsync(model.Id, ct) is not { } permission)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var result = await permissions.UpdateAsync(new PermissionEditInput(model.Id, model.Icon, model.Sort, model.IsEnabled), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已保存权限点 {permission.PermName}";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        model.PermCode = permission.PermCode;
        model.PermName = permission.PermName;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("从代码重建")]
    [OpLog(Module, "从代码重建权限树")]
    public async Task<IActionResult> Rebuild(CancellationToken ct)
    {
        var report = await rebuildService.RebuildAsync(AreaCatalog.All, ct);
        TempData["Message"] = $"重建完成：新增 {report.Added} 个，更新 {report.Updated} 个，停用 {report.Disabled} 个"
                              + (report.DisabledCodes.Count > 0 ? $"。停用的权限码：{string.Join("、", report.DisabledCodes)}" : string.Empty)
                              + (report.ReEnabledCodes.Count > 0 ? $"。重新启用的权限码：{string.Join("、", report.ReEnabledCodes)}" : string.Empty);
        if (report.MissingMenuControllers.Count > 0)
        {
            TempData["Error"] = $"以下 Controller 缺少 [PermMenu]，菜单名临时用了类名：{string.Join("、", report.MissingMenuControllers)}";
        }

        return RedirectToAction(nameof(Index));
    }
}
