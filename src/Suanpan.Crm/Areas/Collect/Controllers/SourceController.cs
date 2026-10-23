using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Common.Localization;
using Suanpan.Crm.Models.Collect;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Collect.Sources.Models;
using Suanpan.Services.Reference.Economies;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Collect.Controllers;

/// <summary>
/// 数据源（采集设计 §8.1，FR-001）。新建为停用状态，启用时检查合规信息（BR-002）；凭据加密保存，页面只显示是否已设置（BR-004）；
/// 被引用的数据源不能删除（BR-006）。连通性测试在详情页执行，结果经 TempData 带回详情页。
/// </summary>
[Area("Collect")]
[PermMenu("数据源", Icon = "plug-connected", Sort = 10)]
public sealed class SourceController(IDataSourceService sources, IEconomyService economies) : Controller
{
    private const string Module = "数据采集";
    private const string TestResultKey = "SourceTestResult";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await sources.GetListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        if (await sources.GetAsync(id, ct) is not { } source)
        {
            return NotFound();
        }

        var testResult = TempData[TestResultKey] is string json ? JsonSerializer.Deserialize<DataSourceTestResult>(json) : null;
        var economy = source.EconomyCode is { } code ? await economies.GetAsync(code, ct) : null;
        return View(new DataSourceDetailViewModel { Source = source, EconomyName = economy?.Name.Get(ContentLanguage.Zh), TestResult = testResult });
    }

    /// <summary>超时默认 30 秒（PRD FR-002 异常流程"请求失败"）</summary>
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
        => View("Edit", await WithOptionsAsync(new DataSourceEditViewModel { TimeoutSeconds = DataSourceRule.DefaultTimeoutSeconds }, existing: null, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "新增数据源")]
    public async Task<IActionResult> Create(DataSourceEditViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await sources.CreateAsync(ToInput(model), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已新增数据源 {model.Name!.Trim()}，当前为停用状态，测试连通性后再启用";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View("Edit", await WithOptionsAsync(model, existing: null, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        if (await sources.GetAsync(id, ct) is not { } source)
        {
            return NotFound();
        }

        var model = new DataSourceEditViewModel
        {
            Name = source.Name,
            Agency = source.Agency,
            EconomyCode = source.EconomyCode,
            Type = source.Type,
            BaseUrl = source.BaseUrl,
            RateLimit = source.RateLimit,
            RateLimitUnit = source.RateLimitUnit,
            TimeoutSeconds = source.TimeoutSeconds,
            CredentialRequired = source.CredentialRequired,
            ComplianceClass = source.ComplianceClass,
            TermsUrl = source.TermsUrl,
            RobotsResult = source.RobotsResult,
            Remark = source.Remark,
        };
        return View(await WithOptionsAsync(model, source, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "编辑数据源")]
    public async Task<IActionResult> Edit(long id, DataSourceEditViewModel model, CancellationToken ct)
    {
        if (await sources.GetAsync(id, ct) is not { } source)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var result = await sources.UpdateAsync(id, ToInput(model), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已保存数据源 {model.Name!.Trim()}";
                return RedirectToAction(nameof(Detail), new { id });
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View(await WithOptionsAsync(model, source, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("启用 / 停用")]
    [OpLog(Module, "启用或停用数据源")]
    public async Task<IActionResult> SetStatus(long id, bool enabled, CancellationToken ct)
        => Done(await sources.SetEnabledAsync(id, enabled, ct), enabled ? "数据源已启用" : "数据源已停用，它下面的采集任务将停止执行");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "删除数据源")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
        => Done(await sources.DeleteAsync(id, ct), "数据源已删除");

    /// <summary>连通性测试（PRD FR-001 主流程第 2 步）：失败时写 TempData["Error"]，操作日志记为失败；结果详情显示在详情页</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("连通性测试")]
    [OpLog(Module, "测试数据源连通性")]
    public async Task<IActionResult> Test(long id, CancellationToken ct)
    {
        var result = await sources.TestAsync(id, ct);
        TempData[TestResultKey] = JsonSerializer.Serialize(result);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? "连通性测试成功" : $"连通性测试失败：{result.Error}";
        return RedirectToAction(nameof(Detail), new { id });
    }

    private static DataSourceInput ToInput(DataSourceEditViewModel model) => new()
    {
        Name = model.Name!,
        Agency = model.Agency!,
        EconomyCode = model.EconomyCode,
        Type = model.Type!.Value,
        BaseUrl = model.BaseUrl!,
        RateLimit = model.RateLimit!.Value,
        RateLimitUnit = model.RateLimitUnit!.Value,
        TimeoutSeconds = model.TimeoutSeconds!.Value,
        CredentialRequired = model.CredentialRequired,
        Credential = model.Credential,
        ClearCredential = model.ClearCredential,
        ComplianceClass = model.ComplianceClass!.Value,
        TermsUrl = model.TermsUrl!,
        RobotsResult = model.RobotsResult!.Value,
        Remark = model.Remark,
    };

    /// <summary>经济体只列启用的（BR-028）；编辑时保留数据源当前的经济体，即使它已停用</summary>
    private async Task<DataSourceEditViewModel> WithOptionsAsync(DataSourceEditViewModel model, DataSource? existing, CancellationToken ct)
    {
        var economyOptions = (await economies.GetOptionsAsync(ct))
            .Select(e => new SelectListItem($"{e.Code} {e.Name.Get(ContentLanguage.Zh)}", e.Code))
            .ToList();
        if (existing?.EconomyCode is { } current
            && economyOptions.TrueForAll(o => o.Value != current)
            && await economies.GetAsync(current, ct) is { } economy)
        {
            economyOptions.Insert(0, new SelectListItem($"{economy.Code} {economy.Name.Get(ContentLanguage.Zh)}（已停用）", economy.Code));
        }

        model.Options = new DataSourceFormOptions
        {
            Economies = economyOptions,
            Types = EnumOptions<DataSourceType>(),
            RateLimitUnits = EnumOptions<RateLimitUnit>(),
            ComplianceClasses = EnumOptions<ComplianceClass>(),
            RobotsResults = EnumOptions<RobotsResult>(),
            HasCredential = existing?.Credential is not null,
        };
        return model;
    }

    /// <summary>枚举的下拉框选项：值为枚举名，文字为中文显示名（CollectDisplayNames）</summary>
    private static List<SelectListItem> EnumOptions<TEnum>()
        where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().Select(value => new SelectListItem(CollectDisplayNames.Get(value), value.ToString())).ToList();

    private IActionResult Done(ManageResult result, string message)
    {
        TempData[result.Success ? "Message" : "Error"] = result.Success ? message : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
