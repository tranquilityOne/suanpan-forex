using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Suanpan.Crm.Auth;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Shared.Auth;
using Xunit;

namespace Suanpan.Crm.Tests.Auth;

/// <summary>栏目清单和 Area 内页面的约定（sys 设计 §6.2、§9.1）</summary>
public class AreaCatalogTests
{
    /// <summary>权限树重建只为栏目清单中的 Area 建目录；漏登记的 Area，页面不会出现在权限树和菜单中</summary>
    [Fact]
    public async Task EveryAreaWithPagesIsInCatalog()
    {
        await using var app = new CrmApp();
        var catalog = AreaCatalog.All.Select(a => a.Area).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var areas = AreaActions(app).Select(d => d.RouteValues["area"]).Distinct().ToList();

        Assert.NotEmpty(areas);
        Assert.All(areas, area => Assert.Contains(area!, catalog));
    }

    /// <summary>Area 内的 Controller 必须标 [PermMenu]，否则重建出的菜单没有中文名和图标</summary>
    [Fact]
    public async Task EveryAreaControllerHasPermMenu()
    {
        await using var app = new CrmApp();

        var controllers = AreaActions(app).Select(d => d.ControllerTypeInfo).Distinct().ToList();

        Assert.All(controllers, type => Assert.NotNull(type.GetCustomAttribute<PermMenuAttribute>()));
    }

    /// <summary>系统管理栏目的权限码与 sys 设计 §9.1 一致（权限码由 Area、Controller、Action 名自动生成，改名会改变权限码）</summary>
    [Fact]
    public async Task SysPagesHaveDocumentedPermCodes()
    {
        await using var app = new CrmApp();
        string[] documented =
        [
            "sys:user:index", "sys:user:create", "sys:user:edit", "sys:user:set_status",
            "sys:user:reset_password", "sys:user:reset_totp", "sys:user:unlock",
            "sys:role:index", "sys:role:create", "sys:role:edit", "sys:role:delete", "sys:role:assign", "sys:role:apply_defaults",
            "sys:permission:index", "sys:permission:edit", "sys:permission:rebuild",
        ];

        var codes = AreaActions(app)
            .SelectMany(d => d.EndpointMetadata.OfType<HasPermissionAttribute>())
            .Select(p => p.Policy![HasPermissionAttribute.PolicyPrefix.Length..])
            .ToHashSet();

        Assert.All(documented, code => Assert.Contains(code, codes));
    }

    private static IEnumerable<ControllerActionDescriptor> AreaActions(CrmApp app)
        => app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(d => d.RouteValues.TryGetValue("area", out var area) && !string.IsNullOrEmpty(area));
}
