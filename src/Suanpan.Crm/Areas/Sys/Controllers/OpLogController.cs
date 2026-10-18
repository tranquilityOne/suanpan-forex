using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Suanpan.Crm.Models.Sys;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Sys.Logging;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Sys.Controllers;

/// <summary>
/// 操作日志（sys 设计 §7.3，FR-029）：只能查询和导出，没有修改、删除入口。
/// 导出动作本身记入操作日志（BR-150）。
/// </summary>
[Area("Sys")]
[PermMenu("操作日志", Icon = "list-details", Sort = 60)]
public sealed class OpLogController(
    IOpLogService opLogs,
    IOperatorTimeZone operatorTimeZone,
    TimeProvider timeProvider,
    IActionDescriptorCollectionProvider actionDescriptors) : Controller
{
    private const string Module = "系统管理";

    [HttpGet]
    public async Task<IActionResult> Index(OpLogFilter filter, int page = 1, CancellationToken ct = default)
        => View(new OpLogListViewModel
        {
            Filter = filter,
            Result = await opLogs.QueryAsync(filter.ToQuery(operatorTimeZone.TimeZone, page), ct),
            ActionsByModule = ActionsByModule(),
        });

    [HttpGet]
    [PermName("导出")]
    [OpLog(Module, "导出操作日志")]
    public async Task<IActionResult> Export(OpLogFilter filter, CancellationToken ct)
    {
        var timeZone = operatorTimeZone.TimeZone;
        var stamp = TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, timeZone).ToString("yyyyMMdd-HHmmss");
        CsvDownload.Begin(Response, $"操作日志-{stamp}.csv", $"op-log-{stamp}.csv");
        await opLogs.ExportAsync(filter.ToQuery(timeZone), Response.Body, timeZone, ct);
        return new EmptyResult();
    }

    /// <summary>代码中标了 [OpLog] 的全部动作，按模块分组；日志里的模块和动作就是这些名字</summary>
    private List<IGrouping<string, string>> ActionsByModule()
        => actionDescriptors.ActionDescriptors.Items
            .SelectMany(a => a.EndpointMetadata.OfType<OpLogAttribute>())
            .Select(o => (o.Module, o.Action))
            .Distinct()
            .OrderBy(o => o.Module, StringComparer.Ordinal)
            .ThenBy(o => o.Action, StringComparer.Ordinal)
            .GroupBy(o => o.Module, o => o.Action)
            .ToList();
}
