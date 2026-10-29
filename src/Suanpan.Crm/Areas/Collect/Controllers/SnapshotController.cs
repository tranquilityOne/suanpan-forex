using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Net.Http.Headers;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Crm.Models.Collect;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Runs.Models;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Collect.Tasks;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Collect.Controllers;

/// <summary>
/// 快照（采集设计 §5.5、§5.6、§8.1，FR-004）：查看、下载、重新解析，只有管理员和采集工程师能用（PRD §13.1.2）。
/// 内容只以纯文本显示（BR-025），下载一律按附件。重新解析按任务进行：先在列表中选定任务，勾选快照或用全部筛选结果，
/// 在表单页选择解析器、确认快照数后提交，跳到运行详情
/// </summary>
[Area("Collect")]
[PermMenu("快照", Icon = "file-search", Sort = 40)]
public sealed class SnapshotController(
    ISnapshotService snapshots,
    ICollectRunService runs,
    ICollectTaskService tasks,
    IDataSourceService sources,
    IParserCatalog parsers,
    IOperatorTimeZone operatorTimeZone,
    ICurrentUser currentUser) : Controller
{
    private const string Module = "数据采集";

    [HttpGet]
    public async Task<IActionResult> Index(SnapshotFilter filter, int page = 1, CancellationToken ct = default)
        => View(new SnapshotListViewModel
        {
            Filter = filter,
            Result = await snapshots.QueryAsync(filter.ToQuery(operatorTimeZone.TimeZone, page), ct),
            TaskOptions = (await tasks.GetAllAsync(ct)).Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToList(),
            SourceOptions = (await sources.GetListAsync(ct)).Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList(),
            RunStatusOptions = CollectEnumOptions.Of<CollectRunStatus>(),
        });

    [HttpGet]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
        => await snapshots.GetAsync(id, ct) is { } detail ? View(detail) : NotFound();

    /// <summary>原始内容按附件下载，浏览器不打开、不按内容猜类型（采集设计 §5.5）</summary>
    [HttpGet]
    public async Task<IActionResult> Download(long id, CancellationToken ct)
    {
        if (await snapshots.GetAsync(id, ct) is not { Content: { } content } detail)
        {
            return NotFound();
        }

        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return File(content, "application/octet-stream", SnapshotContentView.FileName(id, detail.Snapshot.ContentType));
    }

    /// <summary>重新解析的表单页：显示会处理的快照数，选择解析器（默认是任务的解析器）</summary>
    [HttpGet]
    public async Task<IActionResult> Reparse(SnapshotFilter filter, List<long> ids, CancellationToken ct)
    {
        var model = new SnapshotReparseViewModel { Filter = filter, Ids = ids };
        if (await FillAsync(model, ct) is not { } task)
        {
            TempData["Error"] = "请先选定任务再重新解析";
            return RedirectToAction(nameof(Index), filter.ToRouteValues());
        }

        model.ParserKey = task.ParserKey;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog(Module, "重新解析快照")]
    public async Task<IActionResult> Reparse(SnapshotReparseViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await FillAsync(model, ct) is null ? RedirectToAction(nameof(Index)) : View(model);
        }

        var result = await runs.ReparseAsync(Selection(model), model.ParserKey!, currentUser.UserName!, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return await FillAsync(model, ct) is null ? RedirectToAction(nameof(Index)) : View(model);
        }

        TempData["Message"] = result.Published
            ? "已提交重新解析，等待采集服务开始运行"
            : "运行记录已创建，但下发给采集服务失败，稍后由调度自动重新下发";
        return RedirectToAction("Detail", "Run", new { id = result.RunId });
    }

    /// <summary>补上任务名、快照数和解析器选项；没有选定任务或任务不存在时返回 null</summary>
    private async Task<Core.Collect.Entities.CollectTask?> FillAsync(SnapshotReparseViewModel model, CancellationToken ct)
    {
        if (model.Filter.TaskId is not { } taskId || await tasks.GetAsync(taskId, ct) is not { } task)
        {
            return null;
        }

        model.TaskName = task.Name;
        model.Count = await runs.CountForReparseAsync(Selection(model), ct);
        model.Blocked = ReparseRule.ValidateCount(model.Count);
        var taskParser = parsers.Find(task.ParserKey);
        model.ParserOptions = parsers.All
            .Where(p => ReparseRule.ValidateParser(taskParser, p) is null)
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new SelectListItem($"{p.Name}（{p.Key}）", p.Key))
            .ToList();
        return task;
    }

    private ReparseSelection Selection(SnapshotReparseViewModel model)
        => new(model.Filter.ToQuery(operatorTimeZone.TimeZone), model.Ids);
}
