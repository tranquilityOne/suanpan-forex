using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Services.Sys.Security;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Rbac.Manage;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class UserManageServiceTests(PostgresFixture postgres)
{
    private const string ValidPassword = "Correct-Horse-1";

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public async Task Create_MakesPendingAccountThatMustChangePassword()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var username = TestData.Unique("new");

        var result = await Service(db).CreateAsync(
            new UserCreateInput($" {username} ", " 张三 ", " ", ValidPassword, [TestData.EditorRoleId, TestData.EditorRoleId]), Ct);

        Assert.True(result.Success, result.Error);
        var user = await db.SysUsers.AsNoTracking().SingleAsync(u => u.Username == username, Ct);
        Assert.Equal((UserStatus.Pending, true, "张三", null), (user.Status, user.MustChangePassword, user.RealName, user.Email));
        Assert.True(_hasher.Verify(ValidPassword, user.PasswordHash));
        Assert.Equal([TestData.EditorRoleId], await db.SysUserRoles.Where(x => x.UserId == user.Id).Select(x => x.RoleId).ToListAsync(Ct));
    }

    [Fact]
    public async Task Create_RejectsDuplicateUsernameWeakPasswordAndUnknownRole()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var existing = await TestData.AddUserAsync(db, Ct);
        var service = Service(db);

        Assert.False((await service.CreateAsync(new UserCreateInput(existing.Username, "x", null, ValidPassword, []), Ct)).Success);
        Assert.False((await service.CreateAsync(new UserCreateInput(TestData.Unique("u"), "x", null, "short1", []), Ct)).Success);
        Assert.False((await service.CreateAsync(new UserCreateInput(TestData.Unique("u"), "x", null, ValidPassword, [999_999]), Ct)).Success);
    }

    [Fact]
    public async Task Update_ReplacesRolesAndInvalidatesSnapshot()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.ViewerRoleId);
        var permissionService = new PermissionService(db, _cache);
        Assert.Equal(["viewer"], (await permissionService.GetSnapshotAsync(user.Id, Ct)).RoleCodes);

        var result = await Service(db, permissionService).UpdateAsync(new UserUpdateInput(user.Id, "新名字", "a@example.com", [TestData.EditorRoleId]), Ct);

        Assert.True(result.Success, result.Error);
        Assert.Equal(["editor"], (await permissionService.GetSnapshotAsync(user.Id, Ct)).RoleCodes);
        var saved = await db.SysUsers.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct);
        Assert.Equal(("新名字", "a@example.com"), (saved.RealName, saved.Email));
    }

    [Fact]
    public async Task Update_CannotRemoveAdminRoleFromLastAdmin()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var admin = await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.AdminRoleId);

        var result = await Service(db).UpdateAsync(new UserUpdateInput(admin.Id, "x", null, [TestData.ViewerRoleId]), Ct);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Update_CanRemoveAdminRoleWhenAnotherAdminRemains()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var admin = await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.AdminRoleId);
        await TestData.AddUserAsync(db, Ct, UserStatus.Pending, TestData.AdminRoleId); // 待激活的管理员也算

        var result = await Service(db).UpdateAsync(new UserUpdateInput(admin.Id, "x", null, [TestData.ViewerRoleId]), Ct);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task Disable_RejectsSelf()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await TestData.AddUserAsync(db, Ct);

        var result = await Service(db, currentUserId: user.Id).SetStatusAsync(user.Id, UserStatus.Disabled, Ct);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Disable_RejectsLastAdmin()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var admin = await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.AdminRoleId);
        await TestData.AddUserAsync(db, Ct, UserStatus.Disabled, TestData.AdminRoleId); // 已停用的管理员不算

        var result = await Service(db).SetStatusAsync(admin.Id, UserStatus.Disabled, Ct);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Disable_RefreshesStampAndInvalidatesCaches()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var admin = await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.AdminRoleId);
        await TestData.AddUserAsync(db, Ct, UserStatus.Active, TestData.AdminRoleId);
        var permissionService = new PermissionService(db, _cache);
        var stampValidator = new SecurityStampValidator(db, _cache);
        var oldStamp = admin.SecurityStamp;
        Assert.True((await permissionService.GetSnapshotAsync(admin.Id, Ct)).IsSuperAdmin);
        Assert.True(await stampValidator.ValidateAsync(admin.Id, oldStamp, Ct));

        var result = await Service(db, permissionService, stampValidator).SetStatusAsync(admin.Id, UserStatus.Disabled, Ct);

        Assert.True(result.Success, result.Error);
        Assert.False((await permissionService.GetSnapshotAsync(admin.Id, Ct)).IsSuperAdmin);
        Assert.False(await stampValidator.ValidateAsync(admin.Id, oldStamp, Ct));
        Assert.NotEqual(oldStamp, (await db.SysUsers.AsNoTracking().SingleAsync(u => u.Id == admin.Id, Ct)).SecurityStamp);
    }

    [Fact]
    public async Task Enable_RestoresDisabledAccountOnly()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var disabled = await TestData.AddUserAsync(db, Ct, UserStatus.Disabled);
        var pending = await TestData.AddUserAsync(db, Ct, UserStatus.Pending);
        var service = Service(db);

        Assert.True((await service.SetStatusAsync(disabled.Id, UserStatus.Active, Ct)).Success);
        Assert.Equal(UserStatus.Active, (await db.SysUsers.AsNoTracking().SingleAsync(u => u.Id == disabled.Id, Ct)).Status);
        Assert.False((await service.SetStatusAsync(pending.Id, UserStatus.Active, Ct)).Success);
        Assert.False((await service.SetStatusAsync(disabled.Id, UserStatus.Pending, Ct)).Success);
    }

    [Fact]
    public async Task ResetPassword_RequiresPasswordChangeAndRefreshesStamp()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await TestData.AddUserAsync(db, Ct);
        var oldStamp = user.SecurityStamp;

        Assert.False((await Service(db).ResetPasswordAsync(user.Id, "abcdefghijkl", Ct)).Success); // 没有数字
        var result = await Service(db).ResetPasswordAsync(user.Id, ValidPassword, Ct);

        Assert.True(result.Success, result.Error);
        var saved = await db.SysUsers.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct);
        Assert.True(saved.MustChangePassword);
        Assert.True(_hasher.Verify(ValidPassword, saved.PasswordHash));
        Assert.NotEqual(oldStamp, saved.SecurityStamp);
    }

    [Fact]
    public async Task ResetTotp_ClearsSecretAndRefreshesStamp()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await TestData.AddUserAsync(db, Ct);
        Assert.False((await Service(db).ResetTotpAsync(user.Id, Ct)).Success); // 还没有绑定

        var stamp = user.SecurityStamp;
        user.TotpSecret = "enc:xyz";
        await db.SaveChangesAsync(Ct);
        var result = await Service(db).ResetTotpAsync(user.Id, Ct);

        Assert.True(result.Success, result.Error);
        var saved = await db.SysUsers.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct);
        Assert.Null(saved.TotpSecret);
        Assert.NotEqual(stamp, saved.SecurityStamp);
    }

    [Fact]
    public async Task Query_FiltersByKeywordAndStatusWithPaging()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var marker = TestData.Unique("q");
        foreach (var (name, status) in new[] { ("Alice", UserStatus.Active), ("alicia", UserStatus.Disabled), ("bob", UserStatus.Active) })
        {
            db.SysUsers.Add(new Core.Sys.Entities.SysUser { Username = $"{marker}_{name}", RealName = name, PasswordHash = "x", Status = status });
        }

        await db.SaveChangesAsync(Ct);
        var service = Service(db);

        var byKeyword = await service.QueryAsync(new UserQuery($"{marker}_ALI"), Ct);   // 不区分大小写
        Assert.Equal(2, byKeyword.Total);

        var byStatus = await service.QueryAsync(new UserQuery(marker, UserStatus.Active, Page: 2, PageSize: 1), Ct);
        Assert.Equal(2, byStatus.Total);
        Assert.Equal($"{marker}_bob", Assert.Single(byStatus.Items).Username);

        Assert.Equal(0, (await service.QueryAsync(new UserQuery($"{marker}%"), Ct)).Total); // % 按普通字符匹配
    }

    private UserManageService Service(
        SuanpanDbContext db, IPermissionService? permissionService = null, ISecurityStampValidator? stampValidator = null,
        long? currentUserId = null)
        => new(db, _hasher, permissionService ?? new PermissionService(db, _cache),
            stampValidator ?? new SecurityStampValidator(db, _cache), new StubCurrentUser(currentUserId));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
