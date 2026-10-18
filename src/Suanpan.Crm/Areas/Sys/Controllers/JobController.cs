using Microsoft.AspNetCore.Mvc;
using Suanpan.Shared.Auth;

namespace Suanpan.Crm.Areas.Sys.Controllers;

/// <summary>
/// 任务面板入口（sys 设计 §9.1）：菜单项和权限点 sys:job:index 由这个 action 生成，
/// 打开后跳转到 Hangfire 面板；面板本身由端点授权按同一个权限码保护（Program.cs）。
/// </summary>
[Area("Sys")]
[PermMenu("任务面板", Icon = "clock-play", Sort = 80)]
public sealed class JobController : Controller
{
    public const string DashboardPath = "/Sys/hangfire";

    [HttpGet]
    public IActionResult Index() => Redirect(DashboardPath);
}
