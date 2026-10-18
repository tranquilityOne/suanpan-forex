using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Constants;
using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Rbac;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class PermissionServiceTests(PostgresFixture postgres)
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task Admin_IsSuperAdminWithoutPermissionRows()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.AdminRoleId, TestData.ViewerRoleId);

        var snapshot = await new PermissionService(db, _cache).GetSnapshotAsync(user.Id, Ct);

        Assert.True(snapshot.IsSuperAdmin);
        Assert.Equal([RoleCodes.SuperAdmin, RoleCodes.Viewer], snapshot.RoleCodes);
        Assert.Empty(snapshot.Codes);
        Assert.True(snapshot.Has("anything:at:all"));
    }

    [Fact]
    public async Task User_GetsCodesOfEnabledRolesAndEnabledPermissions()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var area = TestData.Unique("a");
        var granted = await TestData.AddPermissionAsync(db, Ct, $"{area}:menu:index", PermType.Menu);
        var disabledPermission = await TestData.AddPermissionAsync(db, Ct, $"{area}:menu:edit", enabled: false);
        var viaDisabledRole = await TestData.AddPermissionAsync(db, Ct, $"{area}:menu:delete");
        var enabledRole = await TestData.AddRoleAsync(db, Ct);
        var disabledRole = await TestData.AddRoleAsync(db, Ct, enabled: false);
        await TestData.GrantAsync(db, Ct, enabledRole.Id, granted.Id, disabledPermission.Id);
        await TestData.GrantAsync(db, Ct, disabledRole.Id, viaDisabledRole.Id);
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, enabledRole.Id, disabledRole.Id);

        var snapshot = await new PermissionService(db, _cache).GetSnapshotAsync(user.Id, Ct);

        Assert.False(snapshot.IsSuperAdmin);
        Assert.Equal([enabledRole.RoleCode], snapshot.RoleCodes);
        Assert.Equal([granted.PermCode], snapshot.Codes);
    }

    [Theory]
    [InlineData(UserStatus.Pending)]
    [InlineData(UserStatus.Disabled)]
    public async Task NonActiveAccount_GetsEmptySnapshot(UserStatus status)
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await TestData.AddUserAsync(db, Ct, status, TestData.AdminRoleId);

        var snapshot = await new PermissionService(db, _cache).GetSnapshotAsync(user.Id, Ct);

        Assert.Same(PermissionSnapshot.Empty, snapshot);
    }

    [Fact]
    public async Task AccountWithoutRoles_GetsEmptySnapshot()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await TestData.AddUserAsync(db, Ct);

        Assert.Same(PermissionSnapshot.Empty, await new PermissionService(db, _cache).GetSnapshotAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Snapshot_IsCachedUntilInvalidated()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (service, user, role, permission) = await ArrangeUserWithOnePermissionAsync(db);
        Assert.True((await service.GetSnapshotAsync(user.Id, Ct)).Has(permission.PermCode));

        await db.SysRolePermissions.Where(x => x.RoleId == role.Id).ExecuteDeleteAsync(Ct);
        Assert.True((await service.GetSnapshotAsync(user.Id, Ct)).Has(permission.PermCode)); // 仍是缓存

        service.Invalidate(user.Id);
        Assert.False((await service.GetSnapshotAsync(user.Id, Ct)).Has(permission.PermCode));
    }

    [Fact]
    public async Task InvalidateRoleHolders_RefreshesAccountsWithThatRole()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (service, user, role, permission) = await ArrangeUserWithOnePermissionAsync(db);
        await service.GetSnapshotAsync(user.Id, Ct);

        await db.SysRolePermissions.Where(x => x.RoleId == role.Id).ExecuteDeleteAsync(Ct);
        await service.InvalidateRoleHoldersAsync(role.Id, Ct);

        Assert.False((await service.GetSnapshotAsync(user.Id, Ct)).Has(permission.PermCode));
    }

    [Fact]
    public async Task InvalidateAll_RefreshesEveryAccount()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var (service, user, role, permission) = await ArrangeUserWithOnePermissionAsync(db);
        await service.GetSnapshotAsync(user.Id, Ct);

        await db.SysRolePermissions.Where(x => x.RoleId == role.Id).ExecuteDeleteAsync(Ct);
        service.InvalidateAll();

        Assert.False((await service.GetSnapshotAsync(user.Id, Ct)).Has(permission.PermCode));
    }

    private async Task<(PermissionService, Core.Sys.Entities.SysUser, Core.Sys.Entities.SysRole, Core.Sys.Entities.SysPermission)>
        ArrangeUserWithOnePermissionAsync(Data.SuanpanDbContext db)
    {
        var permission = await TestData.AddPermissionAsync(db, Ct, $"{TestData.Unique("a")}:menu:index", PermType.Menu);
        var role = await TestData.AddRoleAsync(db, Ct);
        await TestData.GrantAsync(db, Ct, role.Id, permission.Id);
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, role.Id);
        return (new PermissionService(db, _cache), user, role, permission);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
