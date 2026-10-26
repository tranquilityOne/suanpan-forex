using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Crm.Models.Collect;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Runs.Models;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Collect.Tasks;
using Suanpan.Services.Collect.Tasks.Models;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Collect.Controllers;

/// <summary>
/// 采集任务（采集设计 §8.1、§8.2，FR-002）。新建为停用状态，启用时数据源必须启用；有运行记录的任务只能停用（BR-154）。
/// "立即执行"分两个 action：Trigger 只能触发日历排期、公布值类任务（编辑），TriggerAny 不限（PRD §13.1.2），
/// 成功后跳到运行详情。
/// </summary>
[Area("Collect")]
[PermMenu("采集任务", Icon = "clock-play", Sort = 20)]
public sealed class TaskController(
    ICollectTaskService tasks,
    ICollectRunService runs,
    IDataSourceService sources,
    IParserCatalog parsers,
    ICurrentUser currentUser) : Controller
{
    private const string Module = "数据采集";

    [HttpGet]
    public async Task<IActionResult> Index(CollectTaskFilter filter, int page = 1, CancellationToken ct = default)
        => View(new CollectTaskListViewModel
        {
            Filter = filter,
            Result = await tasks.QueryAsync(filter.ToQuery(page), ct),
            SourceOptions = (await sources.GetListAsync(ct)).Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList(),
            TargetTypeOptions = CollectEnumOptions.Of<CollectTargetType>(),
            HasParsers = parsers.All.Count > 0,
        });

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
        => View("Edit", await WithOptionsAsync(new CollectTaskEditViewModel(), existing: null, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "新增采集任务")]
    public async Task<IActionResult> Create(CollectTaskEditViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await tasks.CreateAsync(ToInput(model), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已新增采集任务 {model.Name!.Trim()}，当前为停用状态，可以先立即执行试跑，再启用";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View("Edit", await WithOptionsAsync(model, existing: null, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        if (await tasks.GetAsync(id, ct) is not { } task)
        {
            return NotFound();
        }

        var model = new CollectTaskEditViewModel
        {
            Name = task.Name,
            SourceId = task.SourceId,
            ParserKey = task.ParserKey,
            Cron = task.Cron,
            Params = task.Params,
            Remark = task.Remark,
        };
        return View(await WithOptionsAsync(model, task, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "编辑采集任务")]
    public async Task<IActionResult> Edit(long id, CollectTaskEditViewModel model, CancellationToken ct)
    {
        if (await tasks.GetAsync(id, ct) is not { } task)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var result = await tasks.UpdateAsync(id, ToInput(model), ct);
            if (result.Success)
            {
                TempData["Message"] = $"已保存采集任务 {model.Name!.Trim()}";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(result.Field ?? string.Empty, result.Error!);
        }

        return View(await WithOptionsAsync(model, task, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("启用 / 停用")]
    [OpLog(Module, "启用或停用采集任务")]
    public async Task<IActionResult> SetStatus(long id, bool enabled, CancellationToken ct)
        => Done(await tasks.SetEnabledAsync(id, enabled, ct), enabled ? "采集任务已启用，按 cron 定时执行" : "采集任务已停用，不再定时执行");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "删除采集任务")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
        => Done(await tasks.DeleteAsync(id, ct), "采集任务已删除");

    /// <summary>编辑的"立即执行"：只能触发日历排期、公布值类任务，其他类型由服务拒绝（采集设计 §8.2）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("立即执行（日历排期、公布值）")]
    [OpLog(Module, "立即执行采集任务")]
    public async Task<IActionResult> Trigger(long id, CancellationToken ct)
        => Triggered(await runs.TriggerAsync(id, currentUser.UserName!, anyTargetType: false, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermName("立即执行（全部任务）")]
    [OpLog(Module, "立即执行采集任务")]
    public async Task<IActionResult> TriggerAny(long id, CancellationToken ct)
        => Triggered(await runs.TriggerAsync(id, currentUser.UserName!, anyTargetType: true, ct));

    /// <summary>成功时跳到运行详情，看状态从"排队"变为"运行中"（采集设计 §4.4）；失败时回到任务列表</summary>
    private IActionResult Triggered(EnqueueResult result)
    {
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        TempData["Message"] = result.Published
            ? "已提交执行，等待采集服务开始运行"
            : "运行记录已创建，但下发给采集服务失败，稍后由调度自动重新下发";
        return RedirectToAction("Detail", "Run", new { id = result.RunId });
    }

    private static CollectTaskInput ToInput(CollectTaskEditViewModel model) => new()
    {
        Name = model.Name!,
        SourceId = model.SourceId!.Value,
        ParserKey = model.ParserKey!,
        Cron = model.Cron,
        Params = model.Params,
        Remark = model.Remark,
    };

    private async Task<CollectTaskEditViewModel> WithOptionsAsync(CollectTaskEditViewModel model, CollectTask? existing, CancellationToken ct)
    {
        var sourceOptions = (await sources.GetListAsync(ct))
            .Select(s => new SelectListItem(s.IsEnabled ? s.Name : $"{s.Name}（停用）", s.Id.ToString()))
            .ToList();

        var groups = new Dictionary<string, SelectListGroup>(StringComparer.Ordinal);
        var parserOptions = parsers.All
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new SelectListItem($"{p.Name}（{p.Key}，{CollectDisplayNames.Get(p.TargetType)}）", p.Key)
            {
                Group = groups.TryGetValue(p.AdapterKey, out var group) ? group : groups[p.AdapterKey] = new SelectListGroup { Name = p.AdapterKey },
            })
            .ToList();
        if (existing is not null && parsers.Find(existing.ParserKey) is null)
        {
            parserOptions.Insert(0, new SelectListItem($"{existing.ParserKey}（目录中已没有）", existing.ParserKey));
        }

        model.Options = new CollectTaskFormOptions
        {
            Sources = sourceOptions,
            Parsers = parserOptions,
            TargetType = existing?.TargetType,
            Preview = tasks.PreviewSchedule(model.Cron),
        };
        return model;
    }

    private IActionResult Done(ManageResult result, string message)
    {
        TempData[result.Success ? "Message" : "Error"] = result.Success ? message : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
