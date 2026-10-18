using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Filters;
using Suanpan.Crm.Models.Dev;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Controllers;

/// <summary>
/// 公共部件示例页（/Dev/Components）：展示布局和全部公共部件的用法，并用来在浏览器中验证客户端校验。
/// 只在 Development 环境可用，其他环境返回 404；允许匿名访问，登录页完成前也能打开。
/// </summary>
[AllowAnonymous]
[DevelopmentOnly]
[Route("Dev/Components")]
public sealed class DevComponentsController : Controller
{
    private const int PageSize = 5;

    private static readonly IReadOnlyList<DevSampleRow> SampleRows = Enumerable.Range(1, 12)
        .Select(i => new DevSampleRow(
            i,
            $"示例数据 {i}",
            (UserStatus)(i % 3 + 1),
            new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc).AddHours(-i * 7)))
        .ToList();

    [HttpGet("")]
    public IActionResult Index(int page = 1) => View(Build(page, new DevSampleFormViewModel()));

    [HttpPost("Form")]
    [ValidateAntiForgeryToken]
    [OpLog("开发示例", "提交示例表单")]
    public IActionResult Form([Bind(Prefix = nameof(DevComponentsViewModel.Form))] DevSampleFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(Index), Build(1, form));
        }

        TempData["Message"] = $"已提交：{form.Name}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Disable/{id:long}")]
    [ValidateAntiForgeryToken]
    [OpLog("开发示例", "停用示例数据")]
    public IActionResult Disable(long id)
    {
        TempData["Message"] = $"已停用示例数据 {id}（示例，没有真正修改）";
        return RedirectToAction(nameof(Index));
    }

    private static DevComponentsViewModel Build(int page, DevSampleFormViewModel form)
    {
        page = Math.Max(page, 1);
        var items = SampleRows.Skip((page - 1) * PageSize).Take(PageSize).ToList();
        return new DevComponentsViewModel
        {
            Rows = new PagedResult<DevSampleRow>(items, SampleRows.Count, page, PageSize),
            Form = form,
        };
    }
}
