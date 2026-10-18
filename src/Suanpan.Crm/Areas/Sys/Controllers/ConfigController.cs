using Microsoft.AspNetCore.Mvc;
using Suanpan.Crm.Models.Sys;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Configs.Models;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Sys.Controllers;

/// <summary>
/// 系统配置（sys 设计 §9.1、§10.2）：展示代码中定义的全部配置项，库里没有值的用默认值。
/// 按键编辑：库里没有的配置项第一次保存时新建。保存时按类型校验；Crm 立即生效，其他程序在本机缓存过期后生效（§6.5）。
/// </summary>
[Area("Sys")]
[PermMenu("系统配置", Icon = "adjustments", Sort = 40)]
public sealed class ConfigController(IConfigService configs) : Controller
{
    private const string Module = "系统管理";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await configs.GetListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Edit(string key, CancellationToken ct)
    {
        if (await FindAsync(key, ct) is not { } row)
        {
            return NotFound();
        }

        var model = new ConfigEditViewModel { Key = row.Key, Value = row.Value ?? row.DefaultValue };
        Describe(model, row);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "修改配置")]
    public async Task<IActionResult> Edit(ConfigEditViewModel model, CancellationToken ct)
    {
        if (await FindAsync(model.Key, ct) is not { } row)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var result = await configs.SaveAsync(model.Key, model.Value ?? string.Empty, ct);
            if (result.Success)
            {
                TempData["Message"] = $"已保存 {model.Key}";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(nameof(model.Value), result.Error!);
        }

        Describe(model, row);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("恢复默认值")]
    [OpLog(Module, "恢复配置默认值")]
    public async Task<IActionResult> Reset(string key, CancellationToken ct)
    {
        var result = await configs.ResetAsync(key, ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? $"{key} 已恢复默认值" : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task<ConfigRow?> FindAsync(string key, CancellationToken ct)
        => (await configs.GetListAsync(ct)).FirstOrDefault(r => r.Key == key);

    private static void Describe(ConfigEditViewModel model, ConfigRow row)
    {
        model.ValueType = row.ValueType;
        model.Description = row.Description;
        model.DefaultValue = row.DefaultValue;
        model.IsStored = row.Value is not null;
    }
}
