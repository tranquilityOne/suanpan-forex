using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Suanpan.Crm.Auth;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;

namespace Suanpan.Crm.Tests.Auth;

public class AreaCatalogTests
{
    /// <summary>权限树重建只为栏目清单中的 Area 建目录；漏登记的 Area，页面不会出现在权限树和菜单中</summary>
    [Fact]
    public async Task EveryAreaWithPagesIsInCatalog()
    {
        await using var app = new CrmApp();
        var catalog = AreaCatalog.All.Select(a => a.Area).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var areas = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(d => d.RouteValues.TryGetValue("area", out var area) ? area : null)
            .Where(area => !string.IsNullOrEmpty(area))
            .Distinct()
            .ToList();

        Assert.NotEmpty(areas);
        Assert.All(areas, area => Assert.Contains(area!, catalog));
    }
}
