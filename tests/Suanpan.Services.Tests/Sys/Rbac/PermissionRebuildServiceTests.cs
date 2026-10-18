using System.Reflection;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Models;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Auth;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Rbac;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class PermissionRebuildServiceTests(PostgresFixture postgres)
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    // 每个测试用自己的栏目名，避免并行执行的测试之间在唯一索引上互相等待
    private readonly string _area = "T" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task FirstRebuild_CreatesDirectoryMenusAndButtons()
    {
        await using var db = await postgres.CreateAppContextAsync();

        var report = await Rebuild(db, Endpoints.All(_area));

        Assert.Equal(5, report.Added); // 目录、2 个菜单、2 个按钮
        Assert.Equal(0, report.Updated);
        var area = Snake(_area);
        var rows = await db.SysPermissions.AsNoTracking().Where(p => p.PermCode.StartsWith(area)).ToListAsync(Ct);

        var directory = Assert.Single(rows, p => p.PermType == PermType.Directory);
        Assert.Equal((area, "测试栏目", "flask", 0L), (directory.PermCode, directory.PermName, directory.Icon, directory.ParentId));

        var userMenu = Assert.Single(rows, p => p.PermCode == $"{area}:user:index");
        Assert.Equal((PermType.Menu, "账号管理", $"/{_area}/User", directory.Id), (userMenu.PermType, userMenu.PermName, userMenu.MenuUrl, userMenu.ParentId));

        var resetPassword = Assert.Single(rows, p => p.PermCode == $"{area}:user:reset_password");
        Assert.Equal((PermType.Button, "重置密码", userMenu.Id), (resetPassword.PermType, resetPassword.PermName, resetPassword.ParentId));
        Assert.Single(rows, p => p.PermCode == $"{area}:user:edit" && p.PermName == "编辑"); // 默认名称

        Assert.Equal([$"{_area}/Role"], report.MissingMenuControllers); // Role 控制器没有 [PermMenu]
    }

    [Fact]
    public async Task SecondRebuild_WithSameCode_ChangesNothing()
    {
        await using var db = await postgres.CreateAppContextAsync();
        await Rebuild(db, Endpoints.All(_area));

        var report = await Rebuild(db, Endpoints.All(_area));

        Assert.Equal((0, 0, 0), (report.Added, report.Updated, report.Disabled));
    }

    [Fact]
    public async Task RemovedAction_IsDisabledNotDeleted_AndKeepsIdAndGrants()
    {
        await using var db = await postgres.CreateAppContextAsync();
        await Rebuild(db, Endpoints.All(_area));
        var edit = await db.SysPermissions.AsNoTracking().SingleAsync(p => p.PermCode == $"{Snake(_area)}:user:edit", Ct);
        var role = await TestData.AddRoleAsync(db, Ct);
        await TestData.GrantAsync(db, Ct, role.Id, edit.Id);

        var report = await Rebuild(db, Endpoints.All(_area).Where(e => e.ActionName != "Edit").ToList());

        Assert.Equal([edit.PermCode], report.DisabledCodes);
        var row = await db.SysPermissions.AsNoTracking().SingleAsync(p => p.Id == edit.Id, Ct);
        Assert.False(row.IsEnabled);
        Assert.True(await db.SysRolePermissions.AnyAsync(x => x.RoleId == role.Id && x.PermId == edit.Id, Ct));

        // 代码中恢复后重新启用，id 不变，并在结果中列出
        var restored = await Rebuild(db, Endpoints.All(_area));
        Assert.True((await db.SysPermissions.AsNoTracking().SingleAsync(p => p.Id == edit.Id, Ct)).IsEnabled);
        Assert.Equal([edit.PermCode], restored.ReEnabledCodes);
    }

    [Fact]
    public async Task ChangedMetadata_UpdatesExistingRowInPlace()
    {
        await using var db = await postgres.CreateAppContextAsync();
        await Rebuild(db, Endpoints.All(_area));
        var before = await db.SysPermissions.AsNoTracking().SingleAsync(p => p.PermType == PermType.Directory && p.PermCode == Snake(_area), Ct);

        await Rebuild(db, Endpoints.All(_area), new AreaDirectory(_area, "改名后的栏目", "star", 5));

        var after = await db.SysPermissions.AsNoTracking().SingleAsync(p => p.Id == before.Id, Ct);
        Assert.Equal(("改名后的栏目", "star", 5), (after.PermName, after.Icon, after.Sort));
    }

    [Fact]
    public async Task AreaWithoutEndpoints_GetsNoDirectory()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var emptyArea = "E" + Guid.NewGuid().ToString("N")[..10];

        await Rebuild(db, Endpoints.All(_area), new AreaDirectory(_area, "测试栏目", "flask", 0), new AreaDirectory(emptyArea, "空栏目", null, 1));

        Assert.False(await db.SysPermissions.AnyAsync(p => p.PermCode == Snake(emptyArea), Ct));
    }

    [Fact]
    public async Task Rebuild_InvalidatesMenuCacheAndAllSnapshots()
    {
        await using var db = await postgres.CreateAppContextAsync();
        _cache.Set(MenuService.SourceCacheKey, new List<SysPermission>());
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.ViewerRoleId);
        var permissionService = new PermissionService(db, _cache);
        Assert.Empty((await permissionService.GetSnapshotAsync(user.Id, Ct)).Codes);

        await Rebuild(db, Endpoints.All(_area));
        var userMenu = await db.SysPermissions.AsNoTracking().SingleAsync(p => p.PermCode == $"{Snake(_area)}:user:index", Ct);
        await TestData.GrantAsync(db, Ct, TestData.ViewerRoleId, userMenu.Id);

        Assert.False(_cache.TryGetValue(MenuService.SourceCacheKey, out _));
        Assert.Contains(userMenu.PermCode, (await permissionService.GetSnapshotAsync(user.Id, Ct)).Codes);
    }

    private Task<RebuildReport> Rebuild(SuanpanDbContext db, IReadOnlyList<ControllerActionDescriptor> endpoints, params AreaDirectory[] areas)
    {
        var service = new PermissionRebuildService(
            new StubActionProvider(endpoints), db, new PermissionService(db, _cache), _cache,
            NullLogger<PermissionRebuildService>.Instance);
        return service.RebuildAsync(areas.Length > 0 ? areas : [new AreaDirectory(_area, "测试栏目", "flask", 0)], Ct);
    }

    private static string Snake(string area) => PermCodeHelper.ToSnake(area);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [PermMenu("账号管理", Icon = "users", Sort = 10)]
    private sealed class UserController
    {
        public void Index()
        {
        }

        public void Edit()
        {
        }

        [PermName("重置密码")]
        public void ResetPassword()
        {
        }
    }

    // 故意不标 [PermMenu]
    private sealed class RoleController
    {
        public void Index()
        {
        }
    }

    /// <summary>模拟 AutoPermissionConvention 处理后的端点：每个 action 带上按规则生成的 [HasPermission]</summary>
    private static class Endpoints
    {
        public static List<ControllerActionDescriptor> All(string area) =>
        [
            Action<UserController>(area, "User", "Index"),
            Action<UserController>(area, "User", "Edit"),
            Action<UserController>(area, "User", "Edit"), // GET、POST 同名 action，同一个权限码
            Action<UserController>(area, "User", "ResetPassword"),
            Action<RoleController>(area, "Role", "Index"),
        ];

        private static ControllerActionDescriptor Action<TController>(string area, string controller, string action) => new()
        {
            ControllerName = controller,
            ActionName = action,
            ControllerTypeInfo = typeof(TController).GetTypeInfo(),
            MethodInfo = typeof(TController).GetMethod(action)!,
            RouteValues = new Dictionary<string, string?> { ["area"] = area, ["controller"] = controller, ["action"] = action },
            EndpointMetadata = [new HasPermissionAttribute(PermCodeHelper.Derive(area, controller, action))],
        };
    }

    private sealed class StubActionProvider(IReadOnlyList<ActionDescriptor> items) : IActionDescriptorCollectionProvider
    {
        public ActionDescriptorCollection ActionDescriptors { get; } = new(items, version: 1);
    }
}
