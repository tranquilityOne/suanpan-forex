using Microsoft.AspNetCore.Mvc;
using Suanpan.Core.Sys.Entities;
using Suanpan.Crm.Models.Sys;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Security;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Sys.Controllers;

/// <summary>
/// 后台访问 IP 白名单（sys 设计 §5.5、§9.1，BR-141）。条目可以是单个 IP 或 CIDR 网段；
/// 是否生效由系统配置中的总开关决定。改动 30 秒内对所有 Crm 实例生效（缓存 §6.5）。
/// </summary>
[Area("Sys")]
[PermMenu("IP 白名单", Icon = "shield-lock", Sort = 50)]
public sealed class IpWhitelistController(IIpWhitelistService whitelist, IConfigService configs) : Controller
{
    private const string Module = "系统管理";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var switchEnabled = await configs.GetBoolAsync(
            SysConfigKeys.AdminIpWhitelistEnabled, SysConfigKeys.AdminIpWhitelistEnabledDefault, ct);
        var currentIp = HttpContext.Connection.RemoteIpAddress is { } ip ? IpWhitelistMatcher.Normalize(ip).ToString() : null;
        return View(new IpWhitelistListViewModel(await whitelist.GetListAsync(ct), switchEnabled, currentIp));
    }

    [HttpGet]
    public IActionResult Create() => View("Edit", new IpWhitelistEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "新增 IP 白名单")]
    public Task<IActionResult> Create(IpWhitelistEditViewModel model, CancellationToken ct)
        => SaveAsync(model, $"已新增 {model.IpValue}", ct);

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        if (await whitelist.GetAsync(id, ct) is not { } item)
        {
            return NotFound();
        }

        return View(new IpWhitelistEditViewModel
        {
            Id = item.Id,
            IpValue = item.IpValue,
            Remark = item.Remark,
            IsEnabled = item.IsEnabled,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "编辑 IP 白名单")]
    public Task<IActionResult> Edit(IpWhitelistEditViewModel model, CancellationToken ct)
        => SaveAsync(model, $"已保存 {model.IpValue}", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "删除 IP 白名单")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var result = await whitelist.DeleteAsync(id, ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? "已删除" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(IpWhitelistEditViewModel model, string message, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await whitelist.SaveAsync(new SysIpWhitelist
            {
                Id = model.Id,
                IpValue = model.IpValue!,
                Remark = model.Remark,
                IsEnabled = model.IsEnabled,
            }, ct);
            if (result.Success)
            {
                TempData["Message"] = message;
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        return View("Edit", model);
    }
}
