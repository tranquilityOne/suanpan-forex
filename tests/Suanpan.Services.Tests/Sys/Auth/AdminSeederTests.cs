using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Security;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Auth;

/// <summary>
/// 初始管理员（sys 设计 §3.4）。测试库里没有提交过的账号，每个测试在自己的事务里看到的 sys_user 都是空的
/// （其他测试未提交的账号看不到）。
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class AdminSeederTests(PostgresFixture postgres)
{
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly string _username = TestData.Unique("admin");

    [Fact]
    public async Task CreatesPendingAdmin_WhenThereAreNoAccounts()
    {
        await using var db = await postgres.CreateAppContextAsync();

        Assert.True(await Create(db, _username, "Initial-pass-2026").SeedAsync(Ct));

        var admin = await db.SysUsers.AsNoTracking().SingleAsync(u => u.Username == _username, Ct);
        Assert.Equal((UserStatus.Pending, true, null), (admin.Status, admin.MustChangePassword, admin.TotpSecret));
        Assert.True(_hasher.Verify("Initial-pass-2026", admin.PasswordHash));
        Assert.Equal([TestData.AdminRoleId], await db.SysUserRoles.Where(r => r.UserId == admin.Id).Select(r => r.RoleId).ToListAsync(Ct));
    }

    [Fact]
    public async Task Skips_WhenAccountsExist()
    {
        await using var db = await postgres.CreateAppContextAsync();
        await TestData.AddUserAsync(db, Ct);

        Assert.False(await Create(db, _username, "Initial-pass-2026").SeedAsync(Ct));
        Assert.False(await db.SysUsers.AnyAsync(u => u.Username == _username, Ct));
    }

    [Fact]
    public async Task Skips_WhenNotConfigured()
    {
        await using var db = await postgres.CreateAppContextAsync();

        Assert.False(await Create(db, _username, password: null).SeedAsync(Ct));
        Assert.False(await db.SysUsers.AnyAsync(u => u.Username == _username, Ct));
    }

    private AdminSeeder Create(SuanpanDbContext db, string username, string? password) => new(
        db,
        _hasher,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seed:AdminUsername"] = username,
            ["Seed:AdminPassword"] = password,
        }).Build(),
        NullLogger<AdminSeeder>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
