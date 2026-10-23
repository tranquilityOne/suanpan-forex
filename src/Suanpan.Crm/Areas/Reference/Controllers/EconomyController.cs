using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Core.Reference.Rules;
using Suanpan.Crm.Models.Reference;
using Suanpan.Services.Reference.Currencies;
using Suanpan.Services.Reference.Economies;
using Suanpan.Services.Reference.Economies.Models;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Reference.Controllers;

/// <summary>
/// 经济体（基础数据设计 §6，FR-005）。编码创建后不可修改；货币只能选启用的；
/// 时区只能从 IANA 列表中选（BR-027）；被引用的经济体不能删除，可以停用（BR-028）。
/// </summary>
[Area("Reference")]
[PermMenu("经济体", Icon = "world", Sort = 10)]
public sealed class EconomyController(IEconomyService economies, ICurrencyService currencies) : Controller
{
    private const string Module = "基础数据";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await economies.GetListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var model = new EconomyEditViewModel { Sort = await economies.GetNextSortAsync(ct) };
        return View("Edit", await WithOptionsAsync(model, currentCurrency: null, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "新增经济体")]
    public async Task<IActionResult> Create(EconomyEditViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await economies.CreateAsync(ToInput(model, model.Code!), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已新增经济体 {ReferenceCodeRule.Normalize(model.Code)}";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View("Edit", await WithOptionsAsync(model, currentCurrency: null, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        if (await economies.GetAsync(id, ct) is not { } economy)
        {
            return NotFound();
        }

        var model = new EconomyEditViewModel
        {
            Code = economy.Code,
            NameZh = economy.Name.Zh,
            NameEn = economy.Name.En,
            CurrencyCode = economy.CurrencyCode,
            TimeZone = economy.TimeZone,
            Sort = economy.Sort,
        };
        return View(await WithOptionsAsync(model, economy.CurrencyCode, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "编辑经济体")]
    public async Task<IActionResult> Edit(string id, EconomyEditViewModel model, CancellationToken ct)
    {
        if (await economies.GetAsync(id, ct) is not { } economy)
        {
            return NotFound();
        }

        // 编码不可修改：以库里的为准，表单上只显示
        model.Code = economy.Code;
        ModelState.Remove(nameof(model.Code));
        if (ModelState.IsValid)
        {
            var result = await economies.UpdateAsync(ToInput(model, economy.Code), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已保存经济体 {economy.Code}";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View(await WithOptionsAsync(model, economy.CurrencyCode, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("启用 / 停用")]
    [OpLog(Module, "启用或停用经济体")]
    public async Task<IActionResult> SetStatus(string id, bool enabled, CancellationToken ct)
        => Done(await economies.SetEnabledAsync(id, enabled, ct), $"经济体 {ReferenceCodeRule.Normalize(id)} 已{(enabled ? "启用" : "停用")}");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "删除经济体")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
        => Done(await economies.DeleteAsync(id, ct), $"经济体 {ReferenceCodeRule.Normalize(id)} 已删除");

    private static EconomyInput ToInput(EconomyEditViewModel model, string code)
        => new(code, model.NameZh!, model.NameEn!, model.CurrencyCode!, model.TimeZone!, model.Sort);

    /// <summary>货币下拉框只列启用的（BR-028）；编辑时保留经济体当前使用的货币，即使它已停用</summary>
    private async Task<EconomyEditViewModel> WithOptionsAsync(EconomyEditViewModel model, string? currentCurrency, CancellationToken ct)
    {
        var options = (await currencies.GetOptionsAsync(ct))
            .Select(c => new SelectListItem($"{c.Code} {c.Name.Zh}", c.Code))
            .ToList();
        if (currentCurrency is not null
            && options.TrueForAll(o => o.Value != currentCurrency)
            && await currencies.GetAsync(currentCurrency, ct) is { } current)
        {
            options.Insert(0, new SelectListItem($"{current.Code} {current.Name.Zh}（已停用）", current.Code));
        }

        model.CurrencyOptions = options;
        model.TimeZoneOptions = TimeZoneOptions(model.TimeZone);
        return model;
    }

    /// <summary>IANA 时区按 / 前的大区分组（基础数据设计 §3.2）；当前值不在列表中时补上，避免编辑时被清空</summary>
    private static List<SelectListItem> TimeZoneOptions(string? current)
    {
        var ids = TimeZoneRule.IanaIds.ToList();
        if (TimeZoneRule.IsValid(current) && !ids.Contains(current!))
        {
            ids.Add(current!);
            ids.Sort(StringComparer.Ordinal);
        }

        var groups = new Dictionary<string, SelectListGroup>(StringComparer.Ordinal);
        return ids.Select(id =>
        {
            var slash = id.IndexOf('/');
            var region = slash > 0 ? id[..slash] : id;
            if (!groups.TryGetValue(region, out var group))
            {
                group = new SelectListGroup { Name = region };
                groups.Add(region, group);
            }

            return new SelectListItem(slash > 0 ? id[(slash + 1)..] : id, id) { Group = group };
        }).ToList();
    }

    private IActionResult Done(ManageResult result, string message)
    {
        TempData[result.Success ? "Message" : "Error"] = result.Success ? message : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
