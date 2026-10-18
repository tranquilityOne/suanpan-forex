using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Suanpan.Crm.Controllers;

/// <summary>根目录页面：首页只要求登录（sys 设计 §9.2）；错误页允许匿名，供非开发环境的异常处理使用</summary>
public sealed class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [AllowAnonymous]
    public IActionResult Error()
    {
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
