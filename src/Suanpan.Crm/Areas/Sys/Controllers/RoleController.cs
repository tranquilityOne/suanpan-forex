using Microsoft.AspNetCore.Mvc;
using Suanpan.Crm.Models.Sys;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Sys.Controllers;

/// <summary>
/// 角色管理（sys 设计 §9.1）。内置角色（管理员）直接放行，不可修改、删除和授权；
/// 预置角色可以"恢复默认授权"（§4.5）。授权变更后，持有该角色的账号权限立即生效。
/// </summary>
[Area("Sys")]
[PermMenu("角色管理", Icon = "user-shield", Sort = 20)]
public sealed class RoleController(
    IRoleManageService roles,
    IPermissionManageService permissions,
    IPresetRoleGrantService presetGrants) : Controller
{
    private const string Module = "系统管理";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await roles.GetListAsync(ct));

    [HttpGet]
    public IActionResult Create() => View("Edit", new RoleEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "新建角色")]
    public async Task<IActionResult> Create(RoleEditViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await roles.CreateAsync(new RoleInput(model.RoleCode!, model.RoleName!, model.IsEnabled, model.Remark), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已新建角色 {model.RoleName}，请为它授权";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        if (await roles.GetAsync(id, ct) is not { } role)
        {
            return NotFound();
        }

        if (role.IsBuiltin)
        {
            TempData["Error"] = "内置角色不可修改";
            return RedirectToAction(nameof(Index));
        }

        return View(new RoleEditViewModel
        {
            Id = role.Id,
            RoleCode = role.RoleCode,
            RoleName = role.RoleName,
            IsEnabled = role.IsEnabled,
            Remark = role.Remark,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "编辑角色")]
    public async Task<IActionResult> Edit(RoleEditViewModel model, CancellationToken ct)
    {
        if (await roles.GetAsync(model.Id, ct) is not { } role)
        {
            return NotFound();
        }

        // 角色编码不可修改：以库里的为准，表单上只显示
        model.RoleCode = role.RoleCode;
        ModelState.Remove(nameof(model.RoleCode));
        if (ModelState.IsValid)
        {
            var result = await roles.UpdateAsync(model.Id, new RoleInput(role.RoleCode, model.RoleName!, model.IsEnabled, model.Remark), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已保存角色 {model.RoleName}";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "删除角色")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) => Done(await roles.DeleteAsync(id, ct), "角色已删除");

    [HttpGet]
    [PermName("授权")]
    public async Task<IActionResult> Assign(long id, CancellationToken ct)
    {
        if (await roles.GetAsync(id, ct) is not { } role)
        {
            return NotFound();
        }

        if (role.IsBuiltin)
        {
            TempData["Error"] = "内置角色拥有全部权限，不需要授权";
            return RedirectToAction(nameof(Index));
        }

        return View(new RoleAssignViewModel
        {
            RoleId = role.Id,
            RoleName = role.RoleName,
            Permissions = (await permissions.GetTreeAsync(ct)).Where(p => p.IsEnabled).ToList(),
            GrantedIds = (await roles.GetPermissionIdsAsync(id, ct)).ToHashSet(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("授权")]
    [OpLog(Module, "角色授权")]
    public async Task<IActionResult> Assign(long id, List<long> permIds, CancellationToken ct)
    {
        var result = await roles.SavePermissionsAsync(id, permIds, ct);
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Assign), new { id });
        }

        TempData["Message"] = "授权已保存，持有该角色的账号立即生效";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("恢复默认授权")]
    [OpLog(Module, "恢复默认授权")]
    public async Task<IActionResult> ApplyDefaults(long id, CancellationToken ct)
    {
        if (await roles.GetAsync(id, ct) is not { } role)
        {
            return NotFound();
        }

        return Done(await presetGrants.ApplyDefaultsAsync(role.RoleCode, ct), $"已恢复 {role.RoleName} 的默认授权");
    }

    private IActionResult Done(ManageResult result, string message)
    {
        TempData[result.Success ? "Message" : "Error"] = result.Success ? message : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
