using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Reference.Rules;
using Suanpan.Crm.Models.Reference;
using Suanpan.Services.Reference.Currencies;
using Suanpan.Services.Reference.Economies;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Reference.Indicators.Models;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Reference.Controllers;

/// <summary>
/// 指标字典（基础数据设计 §6，FR-006）。编码和所属经济体创建后不可修改（BR-029）；修改影响方向必须填写理由，
/// 理由记入操作日志（BR-031）；被引用的指标只能停用（BR-153）。没有编辑权限的角色通过详情页查看全部字段。
/// </summary>
[Area("Reference")]
[PermMenu("指标字典", Icon = "list-details", Sort = 30)]
public sealed class IndicatorController(IIndicatorService indicators, IEconomyService economies, ICurrencyService currencies) : Controller
{
    private const string Module = "基础数据";

    [HttpGet]
    public async Task<IActionResult> Index(IndicatorFilter filter, int page = 1, CancellationToken ct = default)
        => View(new IndicatorListViewModel
        {
            Filter = filter,
            Result = await indicators.QueryAsync(filter.ToQuery(page), ct),
            EconomyOptions = (await economies.GetListAsync(ct)).Select(e => EconomyOption(e.Code, e.Name)).ToList(),
            CategoryOptions = EnumOptions<IndicatorCategory>(),
            ImportanceOptions = EnumOptions<Importance>(),
        });

    [HttpGet]
    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        if (await indicators.GetAsync(id, ct) is not { } indicator)
        {
            return NotFound();
        }

        return View(new IndicatorDetailViewModel { Indicator = indicator, Economy = await economies.GetAsync(indicator.EconomyCode, ct) });
    }

    /// <summary>持平容差默认 0（基础数据设计 §2.1）；选了经济体后，页面脚本把货币默认设为经济体的货币</summary>
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
        => View("Edit", await WithOptionsAsync(new IndicatorEditViewModel { FlatTolerance = 0 }, currentCurrency: null, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "新增指标")]
    public async Task<IActionResult> Create(IndicatorEditViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await indicators.CreateAsync(ToInput(model, model.Code!, model.EconomyCode!), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已新增指标 {ReferenceCodeRule.Normalize(model.Code)}";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View("Edit", await WithOptionsAsync(model, currentCurrency: null, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        if (await indicators.GetAsync(id, ct) is not { } indicator)
        {
            return NotFound();
        }

        return View(await WithOptionsAsync(ToModel(indicator), indicator.CurrencyCode, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "编辑指标")]
    public async Task<IActionResult> Edit(string id, IndicatorEditViewModel model, CancellationToken ct)
    {
        if (await indicators.GetAsync(id, ct) is not { } indicator)
        {
            return NotFound();
        }

        // 编码和所属经济体不可修改：以库里的为准，表单上只显示
        (model.Code, model.EconomyCode, model.OriginalDirection) = (indicator.Code, indicator.EconomyCode, indicator.Direction);
        ModelState.Remove(nameof(model.Code));
        ModelState.Remove(nameof(model.EconomyCode));
        if (ModelState.IsValid)
        {
            var result = await indicators.UpdateAsync(ToInput(model, indicator.Code, indicator.EconomyCode), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已保存指标 {indicator.Code}";
                return RedirectToAction(nameof(Detail), new { id = indicator.Code });
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View(await WithOptionsAsync(model, indicator.CurrencyCode, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("启用 / 停用")]
    [OpLog(Module, "启用或停用指标")]
    public async Task<IActionResult> SetStatus(string id, bool enabled, CancellationToken ct)
        => Done(await indicators.SetEnabledAsync(id, enabled, ct), $"指标 {ReferenceCodeRule.Normalize(id)} 已{(enabled ? "启用" : "停用")}");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "删除指标")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
        => Done(await indicators.DeleteAsync(id, ct), $"指标 {ReferenceCodeRule.Normalize(id)} 已删除");

    private static IndicatorInput ToInput(IndicatorEditViewModel model, string code, string economyCode) => new()
    {
        Code = code,
        EconomyCode = economyCode,
        CurrencyCode = model.CurrencyCode!,
        NameZh = model.NameZh!,
        NameEn = model.NameEn!,
        ShortNameZh = model.ShortNameZh!,
        ShortNameEn = model.ShortNameEn!,
        Category = model.Category!.Value,
        ValueType = model.ValueType!.Value,
        Unit = model.Unit!.Value,
        Decimals = model.Decimals!.Value,
        Frequency = model.Frequency!.Value,
        Importance = model.Importance!.Value,
        Direction = model.Direction!.Value,
        FlatTolerance = model.FlatTolerance!.Value,
        RangeMin = model.RangeMin,
        RangeMax = model.RangeMax,
        SigmaN = model.SigmaN,
        AgencyZh = model.AgencyZh!,
        AgencyEn = model.AgencyEn!,
        DefaultReleaseTime = model.DefaultReleaseTime!.Value,
        DescriptionZh = model.DescriptionZh,
        DescriptionEn = model.DescriptionEn,
        Reason = model.Reason,
    };

    private static IndicatorEditViewModel ToModel(Indicator indicator) => new()
    {
        Code = indicator.Code,
        EconomyCode = indicator.EconomyCode,
        CurrencyCode = indicator.CurrencyCode,
        NameZh = indicator.Name.Zh,
        NameEn = indicator.Name.En,
        ShortNameZh = indicator.ShortName.Zh,
        ShortNameEn = indicator.ShortName.En,
        Category = indicator.Category,
        ValueType = indicator.ValueType,
        Unit = indicator.Unit,
        Decimals = indicator.Decimals,
        Frequency = indicator.Frequency,
        Importance = indicator.Importance,
        Direction = indicator.Direction,
        FlatTolerance = indicator.FlatTolerance,
        RangeMin = indicator.RangeMin,
        RangeMax = indicator.RangeMax,
        SigmaN = indicator.SigmaN,
        AgencyZh = indicator.Agency.Zh,
        AgencyEn = indicator.Agency.En,
        DefaultReleaseTime = indicator.DefaultReleaseTime,
        DescriptionZh = indicator.Description?.Zh,
        DescriptionEn = indicator.Description?.En,
        OriginalDirection = indicator.Direction,
    };

    /// <summary>经济体、货币只列启用的（BR-028）；编辑时保留指标当前使用的货币，即使它已停用</summary>
    private async Task<IndicatorEditViewModel> WithOptionsAsync(IndicatorEditViewModel model, string? currentCurrency, CancellationToken ct)
    {
        var enabledEconomies = await economies.GetOptionsAsync(ct);
        var currencyOptions = (await currencies.GetOptionsAsync(ct))
            .Select(c => new SelectListItem($"{c.Code} {c.Name.Get(ContentLanguage.Zh)}", c.Code))
            .ToList();
        if (currentCurrency is not null
            && currencyOptions.TrueForAll(o => o.Value != currentCurrency)
            && await currencies.GetAsync(currentCurrency, ct) is { } current)
        {
            currencyOptions.Insert(0, new SelectListItem($"{current.Code} {current.Name.Get(ContentLanguage.Zh)}（已停用）", current.Code));
        }

        model.Options = new IndicatorFormOptions
        {
            Economies = enabledEconomies.Select(e => EconomyOption(e.Code, e.Name)).ToList(),
            EconomyCurrencies = enabledEconomies.ToDictionary(e => e.Code, e => e.CurrencyCode),
            Currencies = currencyOptions,
            Categories = EnumOptions<IndicatorCategory>(),
            ValueTypes = EnumOptions<IndicatorValueType>(),
            Units = EnumOptions<IndicatorUnit>(),
            Frequencies = EnumOptions<ReleaseFrequency>(),
            Importances = EnumOptions<Importance>(),
            Directions = EnumOptions<ImpactDirection>(),
        };
        return model;
    }

    private static SelectListItem EconomyOption(string code, BilingualText name) => new($"{code} {name.Get(ContentLanguage.Zh)}", code);

    /// <summary>枚举的下拉框选项：值为枚举名，文字为中文显示名（ReferenceDisplayNames）</summary>
    private static List<SelectListItem> EnumOptions<TEnum>()
        where TEnum : struct, Enum
        => Enum.GetValues<TEnum>()
            .Select(value => new SelectListItem(ReferenceDisplayNames.Get(value).Get(ContentLanguage.Zh), value.ToString()))
            .ToList();

    private IActionResult Done(ManageResult result, string message)
    {
        TempData[result.Success ? "Message" : "Error"] = result.Success ? message : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
