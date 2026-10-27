using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Rules;
using Suanpan.Crm.Models.Collect;
using Suanpan.Crm.Models.Reference;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Collect.Tasks;
using Suanpan.Services.Reference.IndicatorSources;
using Suanpan.Services.Reference.IndicatorSources.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Reference.Controllers;

/// <summary>
/// 指标与数据源的映射（基础数据设计 §6.2，FR-006、BR-034）：一个指标配置主源和备源。管理员、采集工程师可以编辑，
/// 其他角色只能查看（PRD §13.1.2）。映射到停用的数据源时照样保存，提示中给出警告（ERR-IND-003）
/// </summary>
[Area("Reference")]
[PermMenu("指标数据源", Icon = "link", Sort = 40)]
public sealed class IndicatorSourceController(
    IIndicatorSourceService mappings,
    IIndicatorService indicators,
    IDataSourceService sources,
    IParserCatalog parsers) : Controller
{
    private const string Module = "基础数据";

    [HttpGet]
    public async Task<IActionResult> Index(IndicatorSourceFilter filter, int page = 1, CancellationToken ct = default)
        => View(new IndicatorSourceListViewModel
        {
            Filter = filter,
            Result = await mappings.QueryAsync(filter.ToQuery(page), ct),
            SourceOptions = (await sources.GetListAsync(ct)).Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList(),
            HasParsers = parsers.All.Count > 0,
        });

    /// <summary>从指标详情页进来时带上指标编码；优先级默认为主源</summary>
    [HttpGet]
    public async Task<IActionResult> Create(string? indicatorCode, CancellationToken ct)
        => View("Edit", await WithOptionsAsync(
            new IndicatorSourceEditViewModel { IndicatorCode = indicatorCode, Priority = IndicatorSourceRule.MainPriority }, existing: null, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "新增指标数据源映射")]
    public async Task<IActionResult> Create(IndicatorSourceEditViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.IndicatorCode))
        {
            ModelState.AddModelError(nameof(model.IndicatorCode), "请选择指标");
        }

        if (ModelState.IsValid)
        {
            var result = await mappings.CreateAsync(ToInput(model, model.IndicatorCode!), ct);
            if (result.Success)
            {
                return await SavedAsync(model, ReferenceCodeRule.Normalize(model.IndicatorCode), ct);
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View("Edit", await WithOptionsAsync(model, existing: null, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        if (await mappings.GetAsync(id, ct) is not { } mapping)
        {
            return NotFound();
        }

        var model = new IndicatorSourceEditViewModel
        {
            IndicatorCode = mapping.IndicatorCode,
            SourceId = mapping.SourceId,
            ExternalId = mapping.ExternalId,
            ParserKey = mapping.ParserKey,
            Priority = mapping.Priority,
        };
        return View(await WithOptionsAsync(model, mapping, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "编辑指标数据源映射")]
    public async Task<IActionResult> Edit(long id, IndicatorSourceEditViewModel model, CancellationToken ct)
    {
        if (await mappings.GetAsync(id, ct) is not { } mapping)
        {
            return NotFound();
        }

        model.IndicatorCode = mapping.IndicatorCode;
        if (ModelState.IsValid)
        {
            var result = await mappings.UpdateAsync(id, ToInput(model, mapping.IndicatorCode), ct);
            if (result.Success)
            {
                return await SavedAsync(model, mapping.IndicatorCode, ct);
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View(await WithOptionsAsync(model, mapping, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "删除指标数据源映射")]
    public async Task<IActionResult> Delete(long id, string? returnIndicatorCode, CancellationToken ct)
    {
        var result = await mappings.DeleteAsync(id, ct);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? "数据源映射已删除" : result.Error;
        return RedirectToAction(nameof(Index), new { IndicatorCode = returnIndicatorCode });
    }

    /// <summary>保存后回到这个指标的映射列表；数据源已停用时在提示中警告（ERR-IND-003）</summary>
    private async Task<IActionResult> SavedAsync(IndicatorSourceEditViewModel model, string indicatorCode, CancellationToken ct)
    {
        var source = await sources.GetAsync(model.SourceId!.Value, ct);
        TempData["Message"] = source is { IsEnabled: false }
            ? $"已保存 {indicatorCode} 的数据源映射。注意：数据源 {source.Name} 已停用，启用前不会从它采集"
            : $"已保存 {indicatorCode} 的数据源映射";
        return RedirectToAction(nameof(Index), new { IndicatorCode = indicatorCode });
    }

    private static IndicatorSourceInput ToInput(IndicatorSourceEditViewModel model, string indicatorCode) => new()
    {
        IndicatorCode = indicatorCode,
        SourceId = model.SourceId!.Value,
        ExternalId = model.ExternalId!,
        ParserKey = model.ParserKey!,
        Priority = model.Priority!.Value,
    };

    private async Task<IndicatorSourceEditViewModel> WithOptionsAsync(IndicatorSourceEditViewModel model, IndicatorSource? existing, CancellationToken ct)
    {
        List<SelectListItem> indicatorOptions;
        if (existing is null)
        {
            indicatorOptions = (await indicators.GetOptionsAsync(ct))
                .Select(i => new SelectListItem($"{i.Code} {i.ShortName.Get(ContentLanguage.Zh)}", i.Code))
                .ToList();
        }
        else
        {
            var indicator = await indicators.GetAsync(existing.IndicatorCode, ct);
            indicatorOptions = [new SelectListItem($"{existing.IndicatorCode} {indicator?.ShortName.Get(ContentLanguage.Zh)}".TrimEnd(), existing.IndicatorCode)];
        }

        var sourceOptions = (await sources.GetListAsync(ct))
            .Select(s => new SelectListItem(s.IsEnabled ? s.Name : $"{s.Name}（停用）", s.Id.ToString()))
            .ToList();

        model.Options = new IndicatorSourceFormOptions
        {
            Indicators = indicatorOptions,
            Sources = sourceOptions,
            Parsers = ParserSelectOptions.Build(parsers, existing?.ParserKey),
        };
        return model;
    }
}
