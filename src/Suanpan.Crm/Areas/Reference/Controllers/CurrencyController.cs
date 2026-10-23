using Microsoft.AspNetCore.Mvc;
using Suanpan.Core.Reference.Rules;
using Suanpan.Crm.Models.Reference;
using Suanpan.Services.Reference.Currencies;
using Suanpan.Services.Reference.Currencies.Models;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Reference.Controllers;

/// <summary>
/// 货币（基础数据设计 §6，FR-005）。编码创建后不可修改；停用后不能再被新选用；
/// 被经济体使用的货币不能删除，可以停用（BR-028）。
/// </summary>
[Area("Reference")]
[PermMenu("货币", Icon = "currency-dollar", Sort = 20)]
public sealed class CurrencyController(ICurrencyService currencies) : Controller
{
    private const string Module = "基础数据";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await currencies.GetListAsync(ct));

    [HttpGet]
    public IActionResult Create() => View("Edit", new CurrencyEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "新增货币")]
    public async Task<IActionResult> Create(CurrencyEditViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await currencies.CreateAsync(new CurrencyInput(model.Code!, model.NameZh!, model.NameEn!), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已新增货币 {ReferenceCodeRule.Normalize(model.Code)}";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        if (await currencies.GetAsync(id, ct) is not { } currency)
        {
            return NotFound();
        }

        return View(new CurrencyEditViewModel { Code = currency.Code, NameZh = currency.Name.Zh, NameEn = currency.Name.En });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "编辑货币")]
    public async Task<IActionResult> Edit(string id, CurrencyEditViewModel model, CancellationToken ct)
    {
        if (await currencies.GetAsync(id, ct) is not { } currency)
        {
            return NotFound();
        }

        // 编码不可修改：以库里的为准，表单上只显示
        model.Code = currency.Code;
        ModelState.Remove(nameof(model.Code));
        if (ModelState.IsValid)
        {
            var result = await currencies.UpdateAsync(new CurrencyInput(currency.Code, model.NameZh!, model.NameEn!), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已保存货币 {currency.Code}";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("启用 / 停用")]
    [OpLog(Module, "启用或停用货币")]
    public async Task<IActionResult> SetStatus(string id, bool enabled, CancellationToken ct)
        => Done(await currencies.SetEnabledAsync(id, enabled, ct), $"货币 {ReferenceCodeRule.Normalize(id)} 已{(enabled ? "启用" : "停用")}");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "删除货币")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
        => Done(await currencies.DeleteAsync(id, ct), $"货币 {ReferenceCodeRule.Normalize(id)} 已删除");

    private IActionResult Done(ManageResult result, string message)
    {
        TempData[result.Success ? "Message" : "Error"] = result.Success ? message : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
