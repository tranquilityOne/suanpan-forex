using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Rbac.Manage;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class PermissionManageServiceTests(PostgresFixture postgres)
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task Tree_IsOrderedDepthFirstBySort()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var area = TestData.Unique("a");
        var directory = await TestData.AddPermissionAsync(db, Ct, area, PermType.Directory);
        var second = await TestData.AddPermissionAsync(db, Ct, $"{area}:b:index", PermType.Menu, directory.Id, sort: 2);
        var first = await TestData.AddPermissionAsync(db, Ct, $"{area}:a:index", PermType.Menu, directory.Id, sort: 1);
        var button = await TestData.AddPermissionAsync(db, Ct, $"{area}:a:edit", PermType.Button, first.Id);

        var rows = (await new PermissionManageService(db, _cache, new PermissionService(db, _cache)).GetTreeAsync(Ct))
            .Where(r => r.PermCode.StartsWith(area)).ToList();

        Assert.Equal([directory.Id, first.Id, button.Id, second.Id], rows.Select(r => r.Id));
        Assert.Equal([0, 1, 2, 1], rows.Select(r => r.Depth));
    }

    [Fact]
    public async Task Update_ChangesOnlyIconSortEnabled_AndInvalidatesCaches()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var menu = await TestData.AddPermissionAsync(db, Ct, $"{TestData.Unique("a")}:x:index", PermType.Menu);
        var role = await TestData.AddRoleAsync(db, Ct);
        await TestData.GrantAsync(db, Ct, role.Id, menu.Id);
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, role.Id);
        var permissionService = new PermissionService(db, _cache);
        Assert.Contains(menu.PermCode, (await permissionService.GetSnapshotAsync(user.Id, Ct)).Codes);
        _cache.Set(MenuService.SourceCacheKey, new List<SysPermission>());

        var result = await new PermissionManageService(db, _cache, permissionService)
            .UpdateAsync(new PermissionEditInput(menu.Id, " star ", 9, IsEnabled: false), Ct);

        Assert.True(result.Success, result.Error);
        var saved = await db.SysPermissions.AsNoTracking().SingleAsync(p => p.Id == menu.Id, Ct);
        Assert.Equal(("star", 9, false, menu.PermCode, menu.PermName), (saved.Icon, saved.Sort, saved.IsEnabled, saved.PermCode, saved.PermName));
        Assert.False(_cache.TryGetValue(MenuService.SourceCacheKey, out _));
        Assert.DoesNotContain(menu.PermCode, (await permissionService.GetSnapshotAsync(user.Id, Ct)).Codes);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
