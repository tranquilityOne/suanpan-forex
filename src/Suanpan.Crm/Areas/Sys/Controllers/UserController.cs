using Microsoft.AspNetCore.Mvc;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Models.Sys;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Sys.Controllers;

/// <summary>
/// 账号管理（sys 设计 §9.1）。没有删除：离职一律停用。停用、重置密码、重置二次验证会让该账号已登录的会话立即失效；
/// 不能停用自己和最后一个管理员（BR-144），由服务判断。
/// </summary>
[Area("Sys")]
[PermMenu("账号管理", Icon = "users", Sort = 10)]
public sealed class UserController(IUserManageService users, IRoleManageService roles, TimeProvider timeProvider) : Controller
{
    private const string Module = "系统管理";

    [HttpGet]
    public async Task<IActionResult> Index(string? keyword, UserStatus? status, int page = 1, CancellationToken ct = default)
        => View(new UserListViewModel
        {
            Keyword = keyword,
            Status = status,
            Result = await users.QueryAsync(new UserQuery(keyword, status, page), ct),
            Now = timeProvider.GetUtcNow().UtcDateTime,
        });

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
        => View(new UserCreateViewModel { RoleOptions = await RoleOptionsAsync(ct) });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "新建账号")]
    public async Task<IActionResult> Create(UserCreateViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await users.CreateAsync(
                new UserCreateInput(model.Username!, model.RealName!, model.Email, model.Password!, model.RoleIds), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已新建账号 {model.Username!.Trim()}，请把初始密码线下告知本人";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        model.RoleOptions = await RoleOptionsAsync(ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        if (await users.GetAsync(id, ct) is not { } user)
        {
            return NotFound();
        }

        return View(new UserEditViewModel
        {
            Id = user.Id,
            Username = user.Username,
            RealName = user.RealName,
            Email = user.Email,
            RoleIds = [.. user.RoleIds],
            RoleOptions = await RoleOptionsAsync(ct),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "编辑账号")]
    public async Task<IActionResult> Edit(UserEditViewModel model, CancellationToken ct)
    {
        if (await users.GetAsync(model.Id, ct) is not { } user)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var result = await users.UpdateAsync(new UserUpdateInput(model.Id, model.RealName!, model.Email, model.RoleIds), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已保存账号 {user.Username}";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        model.Username = user.Username;
        model.RoleOptions = await RoleOptionsAsync(ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("启用 / 停用")]
    [OpLog(Module, "启用或停用账号")]
    public async Task<IActionResult> SetStatus(long id, UserStatus status, CancellationToken ct)
        => Done(await users.SetStatusAsync(id, status, ct), status == UserStatus.Disabled ? "账号已停用，已登录的会话随即失效" : "账号已启用");

    [HttpGet]
    [PermName("重置密码")]
    public async Task<IActionResult> ResetPassword(long id, CancellationToken ct)
        => await users.GetAsync(id, ct) is { } user
            ? View(new ResetPasswordViewModel { Id = user.Id, Username = user.Username })
            : NotFound();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("重置密码")]
    [OpLog(Module, "重置密码")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken ct)
    {
        if (await users.GetAsync(model.Id, ct) is not { } user)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var result = await users.ResetPasswordAsync(model.Id, model.NewPassword!, ct);
            if (result.Success)
            {
                TempData["Message"] = $"已重置 {user.Username} 的密码：请把临时密码线下告知本人，下次登录时必须修改";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        model.Username = user.Username;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("重置二次验证")]
    [OpLog(Module, "重置二次验证")]
    public async Task<IActionResult> ResetTotp(long id, CancellationToken ct)
        => Done(await users.ResetTotpAsync(id, ct), "已重置二次验证，该账号下次登录时需要重新绑定");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("解锁")]
    [OpLog(Module, "解锁账号")]
    public async Task<IActionResult> Unlock(long id, CancellationToken ct)
        => Done(await users.UnlockAsync(id, ct), "账号已解锁，可以立即登录");

    private IActionResult Done(ManageResult result, string message)
    {
        TempData[result.Success ? "Message" : "Error"] = result.Success ? message : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<RoleOption>> RoleOptionsAsync(CancellationToken ct)
        => (await roles.GetListAsync(ct)).Select(r => new RoleOption(r.Id, r.RoleName, r.IsEnabled)).ToList();
}
