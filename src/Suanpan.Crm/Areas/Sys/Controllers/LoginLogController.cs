using Microsoft.AspNetCore.Mvc;
using Suanpan.Crm.Models.Sys;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Sys.Logging;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Areas.Sys.Controllers;

/// <summary>
/// 登录日志（sys 设计 §7.2、§7.3）：密码、二次验证、激活、退出每一步都有记录；只能查询和导出。
/// 导出动作本身记入操作日志（BR-150）。
/// </summary>
[Area("Sys")]
[PermMenu("登录日志", Icon = "login", Sort = 70)]
public sealed class LoginLogController(
    ILoginLogService loginLogs,
    IOperatorTimeZone operatorTimeZone,
    TimeProvider timeProvider) : Controller
{
    private const string Module = "系统管理";

    [HttpGet]
    public async Task<IActionResult> Index(LoginLogFilter filter, int page = 1, CancellationToken ct = default)
        => View(new LoginLogListViewModel
        {
            Filter = filter,
            Result = await loginLogs.QueryAsync(filter.ToQuery(operatorTimeZone.TimeZone, page), ct),
        });

    [HttpGet]
    [PermName("导出")]
    [OpLog(Module, "导出登录日志")]
    public async Task<IActionResult> Export(LoginLogFilter filter, CancellationToken ct)
    {
        var timeZone = operatorTimeZone.TimeZone;
        var stamp = TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, timeZone).ToString("yyyyMMdd-HHmmss");
        CsvDownload.Begin(Response, $"登录日志-{stamp}.csv", $"login-log-{stamp}.csv");
        await loginLogs.ExportAsync(filter.ToQuery(timeZone), Response.Body, timeZone, ct);
        return new EmptyResult();
    }
}
