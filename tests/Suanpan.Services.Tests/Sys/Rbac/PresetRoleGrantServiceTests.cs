using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Constants;
using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Rbac;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class PresetRoleGrantServiceTests(PostgresFixture postgres)
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task ApplyDefaults_ReplacesGrantsWithMatchingEnabledPermissions()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var area = TestData.Unique("a");
        var index = await TestData.AddPermissionAsync(db, Ct, $"{area}:event:index", PermType.Menu);
        var save = await TestData.AddPermissionAsync(db, Ct, $"{area}:forecast:save");
        await TestData.AddPermissionAsync(db, Ct, $"{area}:event:delete");
        await TestData.AddPermissionAsync(db, Ct, $"{area}:forecast:index", enabled: false);
        var stale = await TestData.AddPermissionAsync(db, Ct, $"{area}:old:edit");
        await TestData.GrantAsync(db, Ct, TestData.EditorRoleId, stale.Id);
        var grants = Table(RoleCodes.Editor, $"{area}:*:index", $"{area}:forecast:*");

        var result = await new PresetRoleGrantService(db, new PermissionService(db, _cache), grants).ApplyDefaultsAsync(RoleCodes.Editor, Ct);

        Assert.True(result.Success, result.Error);
        var granted = await db.SysRolePermissions.Where(x => x.RoleId == TestData.EditorRoleId).Select(x => x.PermId).ToListAsync(Ct);
        Assert.Equal([index.Id, save.Id], granted.Order());
    }

    [Fact]
    public async Task ApplyDefaults_InvalidatesHoldersOfTheRole()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var permission = await TestData.AddPermissionAsync(db, Ct, $"{TestData.Unique("a")}:event:index", PermType.Menu);
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.ViewerRoleId);
        var permissionService = new PermissionService(db, _cache);
        Assert.Empty((await permissionService.GetSnapshotAsync(user.Id, Ct)).Codes);

        await new PresetRoleGrantService(db, permissionService, Table(RoleCodes.Viewer, permission.PermCode)).ApplyDefaultsAsync(RoleCodes.Viewer, Ct);

        Assert.Contains(permission.PermCode, (await permissionService.GetSnapshotAsync(user.Id, Ct)).Codes);
    }

    [Theory]
    [InlineData(RoleCodes.SuperAdmin)]
    [InlineData("custom_role")]
    public async Task ApplyDefaults_RejectsRolesOutsideTheTable(string roleCode)
    {
        await using var db = await postgres.CreateAppContextAsync();

        var result = await new PresetRoleGrantService(db, new PermissionService(db, _cache)).ApplyDefaultsAsync(roleCode, Ct);

        Assert.False(result.Success);
    }

    [Fact]
    public void DefaultTable_CoversTheThreePresetRolesOnly()
    {
        Assert.Equal([RoleCodes.Editor, RoleCodes.Engineer, RoleCodes.Viewer], PresetRoleGrants.Patterns.Keys.Order());
    }

    private static Dictionary<string, IReadOnlyList<string>> Table(string roleCode, params string[] patterns)
        => new() { [roleCode] = patterns };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
