using Microsoft.AspNetCore.Mvc;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Tests.Fixtures;

// 只在测试中加载的页面，用来检验管道本身，不依赖业务页面的具体写法。写操作和导出的约定测试不检查这些页面。

/// <summary>Area 内的页面：权限码由 AutoPermissionConvention 自动生成，如 sys:pipeline_probe:index</summary>
[Area("Sys")]
[PermMenu("管道探针")]
public sealed class PipelineProbeController : Controller
{
    public IActionResult Index() => Content("probe");

    /// <summary>测试页面不校验防伪令牌，便于直接 POST</summary>
    [HttpPost]
    [OpLog("测试", "探针保存")]
    public IActionResult Save(string? name) => Content("saved");

    /// <summary>显式标了权限码，不再自动生成 sys:pipeline_probe:export</summary>
    [HasPermission("sys:pipeline_probe:shared")]
    public IActionResult Export() => Content("exported");
}

/// <summary>根目录页面：不进入权限体系，只要求登录</summary>
public sealed class RootProbeController : Controller
{
    public IActionResult Index() => Content("root");
}
