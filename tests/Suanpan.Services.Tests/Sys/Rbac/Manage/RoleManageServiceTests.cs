using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Constants;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Rbac.Manage;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class RoleManageServiceTests(PostgresFixture postgres)
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task List_ShowsPresetRolesWithCounts()
    {
        await using var db = await postgres.CreateAppContextAsync();
        await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.ViewerRoleId);

        var roles = await Service(db).GetListAsync(Ct);

        Assert.Equal(RoleCodes.SuperAdmin, roles[0].RoleCode); // 内置角色排在最前
        Assert.True(roles[0].IsBuiltin);
        Assert.False(roles[0].IsPreset);
        var viewer = Assert.Single(roles, r => r.RoleCode == RoleCodes.Viewer);
        Assert.True(viewer.IsPreset);
        Assert.Equal(1, viewer.UserCount);
    }

    [Theory]
    [InlineData("Upper_case")]
    [InlineData("has,comma")]   // 操作日志里角色编码用逗号分隔
    public async Task Create_RejectsInvalidRoleCode(string roleCode)
    {
        await using var db = await postgres.CreateAppContextAsync();

        Assert.False((await Service(db).CreateAsync(new RoleInput(roleCode, "x", true, null), Ct)).Success);
    }

    [Fact]
    public async Task Create_AddsNonBuiltinRoleAndRejectsDuplicate()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var code = TestData.Unique("analyst");

        Assert.True((await Service(db).CreateAsync(new RoleInput(code, " 分析师 ", true, " "), Ct)).Success);
        Assert.False((await Service(db).CreateAsync(new RoleInput(code, "重复", true, null), Ct)).Success);

        var role = await db.SysRoles.AsNoTracking().SingleAsync(r => r.RoleCode == code, Ct);
        Assert.Equal(("分析师", false, null), (role.RoleName, role.IsBuiltin, role.Remark));
    }

    [Fact]
    public async Task BuiltinRole_CannotBeUpdatedDeletedOrGranted()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Service(db);

        Assert.False((await service.UpdateAsync(TestData.AdminRoleId, new RoleInput("x", "x", false, null), Ct)).Success);
        Assert.False((await service.DeleteAsync(TestData.AdminRoleId, Ct)).Success);
        Assert.False((await service.SavePermissionsAsync(TestData.AdminRoleId, [], Ct)).Success);
    }

    [Fact]
    public async Task Disable_InvalidatesHolders()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var role = await TestData.AddRoleAsync(db, Ct);
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, role.Id);
        var permissionService = new PermissionService(db, _cache);
        Assert.Equal([role.RoleCode], (await permissionService.GetSnapshotAsync(user.Id, Ct)).RoleCodes);

        await Service(db, permissionService).UpdateAsync(role.Id, new RoleInput("ignored", role.RoleName, false, null), Ct);

        Assert.Empty((await permissionService.GetSnapshotAsync(user.Id, Ct)).RoleCodes);
        Assert.Equal(role.RoleCode, (await db.SysRoles.AsNoTracking().SingleAsync(r => r.Id == role.Id, Ct)).RoleCode); // 编码不变
    }

    [Fact]
    public async Task Delete_RequiresNoBoundAccountsAndRemovesGrants()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var role = await TestData.AddRoleAsync(db, Ct);
        var permission = await TestData.AddPermissionAsync(db, Ct, $"{TestData.Unique("a")}:x:index", PermType.Menu);
        await TestData.GrantAsync(db, Ct, role.Id, permission.Id);
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, role.Id);
        var service = Service(db);

        Assert.False((await service.DeleteAsync(role.Id, Ct)).Success);

        await db.SysUserRoles.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(Ct);
        Assert.True((await service.DeleteAsync(role.Id, Ct)).Success);
        Assert.False(await db.SysRoles.AnyAsync(r => r.Id == role.Id, Ct));
        Assert.False(await db.SysRolePermissions.AnyAsync(x => x.RoleId == role.Id, Ct));
    }

    [Fact]
    public async Task SavePermissions_ReplacesGrantsAndInvalidatesHolders()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var area = TestData.Unique("a");
        var first = await TestData.AddPermissionAsync(db, Ct, $"{area}:x:index", PermType.Menu);
        var second = await TestData.AddPermissionAsync(db, Ct, $"{area}:x:edit");
        var role = await TestData.AddRoleAsync(db, Ct);
        await TestData.GrantAsync(db, Ct, role.Id, first.Id);
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, role.Id);
        var permissionService = new PermissionService(db, _cache);
        Assert.Equal([first.PermCode], (await permissionService.GetSnapshotAsync(user.Id, Ct)).Codes);

        var service = Service(db, permissionService);
        Assert.False((await service.SavePermissionsAsync(role.Id, [second.Id, 999_999], Ct)).Success);
        Assert.True((await service.SavePermissionsAsync(role.Id, [second.Id, second.Id], Ct)).Success);

        Assert.Equal([second.Id], await service.GetPermissionIdsAsync(role.Id, Ct));
        Assert.Equal([second.PermCode], (await permissionService.GetSnapshotAsync(user.Id, Ct)).Codes);
    }

    private RoleManageService Service(SuanpanDbContext db, IPermissionService? permissionService = null)
        => new(db, permissionService ?? new PermissionService(db, _cache));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
