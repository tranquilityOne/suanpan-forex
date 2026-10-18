using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Rbac;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class MenuServiceTests(PostgresFixture postgres)
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task User_SeesOnlyPermittedMenusAndTheirDirectories()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var tree = await ArrangeTreeAsync(db);
        var role = await TestData.AddRoleAsync(db, Ct);
        await TestData.GrantAsync(db, Ct, role.Id, tree.UserMenu, tree.UserEditButton);
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, role.Id);

        var menus = await CreateService(db).GetUserMenusAsync(user.Id, Ct);

        var directory = Assert.Single(menus);
        Assert.Equal(tree.SysDirectory, directory.Id);
        var menu = Assert.Single(directory.Children);
        Assert.Equal(tree.UserMenu, menu.Id);
        Assert.Empty(menu.Children); // 按钮不出现在菜单中
    }

    [Fact]
    public async Task Admin_SeesAllEnabledMenusInSortOrder()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var tree = await ArrangeTreeAsync(db);
        var admin = await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.AdminRoleId);

        var menus = await CreateService(db).GetUserMenusAsync(admin.Id, Ct);

        var directory = Assert.Single(menus, m => m.Id == tree.SysDirectory);
        Assert.Equal([tree.RoleMenu, tree.UserMenu], directory.Children.Select(c => c.Id)); // 按 Sort 排序
        Assert.DoesNotContain(menus, m => m.Id == tree.EmptyDirectory);
    }

    [Fact]
    public async Task AccountWithoutPermissions_SeesNothing()
    {
        await using var db = await postgres.CreateAppContextAsync();
        await ArrangeTreeAsync(db);
        var user = await TestData.AddUserAsync(db, Ct);

        Assert.Empty(await CreateService(db).GetUserMenusAsync(user.Id, Ct));
    }

    private MenuService CreateService(SuanpanDbContext db) => new(db, _cache, new PermissionService(db, _cache));

    private sealed record Tree(long SysDirectory, long UserMenu, long RoleMenu, long UserEditButton, long EmptyDirectory);

    /// <summary>目录 → 两个菜单（Role 排在 User 前）→ 按钮；另有一个停用菜单和一个没有可见菜单的目录</summary>
    private static async Task<Tree> ArrangeTreeAsync(SuanpanDbContext db)
    {
        var area = TestData.Unique("a");
        var directory = await TestData.AddPermissionAsync(db, Ct, area, PermType.Directory);
        var userMenu = await TestData.AddPermissionAsync(db, Ct, $"{area}:user:index", PermType.Menu, directory.Id, sort: 20, url: "/Sys/User");
        var roleMenu = await TestData.AddPermissionAsync(db, Ct, $"{area}:role:index", PermType.Menu, directory.Id, sort: 10, url: "/Sys/Role");
        await TestData.AddPermissionAsync(db, Ct, $"{area}:old:index", PermType.Menu, directory.Id, enabled: false);
        var editButton = await TestData.AddPermissionAsync(db, Ct, $"{area}:user:edit", PermType.Button, userMenu.Id);

        var emptyArea = TestData.Unique("e");
        var emptyDirectory = await TestData.AddPermissionAsync(db, Ct, emptyArea, PermType.Directory);
        await TestData.AddPermissionAsync(db, Ct, $"{emptyArea}:x:index", PermType.Menu, emptyDirectory.Id, enabled: false);

        return new Tree(directory.Id, userMenu.Id, roleMenu.Id, editButton.Id, emptyDirectory.Id);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
