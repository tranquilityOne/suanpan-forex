using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Auth.Models;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Security;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Auth;

/// <summary>登录流程（sys 设计 §5.1）。下一步怎么判断由 Core 的 SignInStepRule 测试覆盖，这里测服务把各步骤接起来、写库和写日志</summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class AuthServiceTests(PostgresFixture postgres)
{
    // RFC 6238 附录 B 的 SHA1 密钥（Base32）；Unix 时间 59 秒时的验证码为 287082
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    private const string RfcCode = "287082";

    private const string InitialPassword = "Initial-pass-2026";
    private const string NewPassword = "Brand-new-pass-2026";
    private const string WrongPassword = "Wrong-pass-2026";

    private static readonly LoginClient Client = new("203.0.113.5", "test-agent");

    private readonly FixedTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(59));
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly AesSecretProtector _protector = new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) })
        .Build());

    [Fact]
    public async Task Password_FailuresAreRejectedAndLoggedWithRealReason()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var active = await AddUserAsync(db, UserStatus.Active, mustChangePassword: false, totpBound: true);
        var disabled = await AddUserAsync(db, UserStatus.Disabled, mustChangePassword: false, totpBound: true);
        var unknown = TestData.Unique("nobody");
        var service = Create(db);

        Assert.False((await service.PasswordSignInAsync(unknown, InitialPassword, Client, Ct)).Success);
        Assert.False((await service.PasswordSignInAsync(active.Username, WrongPassword, Client, Ct)).Success);
        Assert.False((await service.PasswordSignInAsync(disabled.Username, InitialPassword, Client, Ct)).Success);

        var logs = await LogsAsync(db, unknown, active.Username, disabled.Username);
        Assert.Equal(3, logs.Count);
        Assert.All(logs, log => Assert.Equal((LoginStage.Password, false, "203.0.113.5"), (log.Stage, log.IsSuccess, log.Ip)));
        Assert.All(logs, log => Assert.False(string.IsNullOrEmpty(log.FailReason)));
        Assert.Null(Assert.Single(logs, log => log.Username == unknown).UserId);
    }

    /// <summary>完成标准（JAN-71）：待激活账号首次登录必须改密码并绑定 TOTP，完成后变为启用</summary>
    [Fact]
    public async Task NewAccount_IsActivatedAfterChangingPasswordAndBindingTotp()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Pending, mustChangePassword: true, totpBound: false);
        var service = Create(db);
        var permissions = new PermissionService(db, _cache);
        Assert.False((await permissions.GetSnapshotAsync(user.Id, Ct)).IsSuperAdmin); // 待激活：空快照，且已缓存

        var password = await service.PasswordSignInAsync(user.Username, InitialPassword, Client, Ct);
        var skipped = await service.ConfirmTotpSetupAsync(user.Id, _protector.Encrypt(RfcSecret), RfcCode, signedIn: false, Client, Ct);
        var changed = await service.ChangePasswordAsync(user.Id, null, NewPassword, signedIn: false, Client, Ct);
        var bound = await service.ConfirmTotpSetupAsync(user.Id, _protector.Encrypt(RfcSecret), RfcCode, signedIn: false, Client, Ct);

        Assert.Equal(SignInStep.ChangePassword, password.Next);
        Assert.False(skipped.Success); // 不能跳过改密码
        Assert.Equal(SignInStep.SetupTotp, changed.Next);
        Assert.Equal(SignInStep.Completed, bound.Next);
        var saved = await db.SysUsers.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct);
        Assert.Equal((UserStatus.Active, false, _time.UtcNow.UtcDateTime), (saved.Status, saved.MustChangePassword, saved.LastLoginAt));
        Assert.Equal(RfcSecret, _protector.Decrypt(saved.TotpSecret!));
        Assert.True(_hasher.Verify(NewPassword, saved.PasswordHash));
        Assert.True((await permissions.GetSnapshotAsync(user.Id, Ct)).IsSuperAdmin); // 激活后快照重新生成
        var stages = (await LogsAsync(db, user.Username)).Where(l => l.IsSuccess).Select(l => l.Stage);
        Assert.Equal([LoginStage.Password, LoginStage.Activation, LoginStage.Activation], stages.Order());
    }

    /// <summary>完成标准（JAN-71）：新密码与旧密码相同时被拒绝；不符合密码规则时被拒绝</summary>
    [Fact]
    public async Task ChangePassword_RejectsSamePasswordAndPolicyViolation()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Active, mustChangePassword: true, totpBound: true);
        var service = Create(db);

        Assert.False((await service.ChangePasswordAsync(user.Id, null, InitialPassword, signedIn: false, Client, Ct)).Success);
        Assert.False((await service.ChangePasswordAsync(user.Id, null, "short1", signedIn: false, Client, Ct)).Success);

        var saved = await db.SysUsers.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct);
        Assert.True(saved.MustChangePassword);
        Assert.True(_hasher.Verify(InitialPassword, saved.PasswordHash));
    }

    /// <summary>完成标准：主动改密码后，其他设备上的旧 Cookie 失效；当前会话拿到新安全戳</summary>
    [Fact]
    public async Task ChangePassword_WhenSignedIn_ChecksCurrentPasswordAndRefreshesStamp()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Active, mustChangePassword: false, totpBound: true);
        var oldStamp = user.SecurityStamp;
        var validator = new SecurityStampValidator(db, _cache);
        Assert.True(await validator.ValidateAsync(user.Id, oldStamp, Ct)); // 旧安全戳已缓存
        var service = Create(db);

        var wrongCurrent = await service.ChangePasswordAsync(user.Id, WrongPassword, NewPassword, signedIn: true, Client, Ct);
        var changed = await service.ChangePasswordAsync(user.Id, InitialPassword, NewPassword, signedIn: true, Client, Ct);

        Assert.False(wrongCurrent.Success);
        Assert.Equal(SignInStep.Completed, changed.Next);
        Assert.False(await validator.ValidateAsync(user.Id, oldStamp, Ct));
        Assert.True(await validator.ValidateAsync(user.Id, changed.User!.SecurityStamp, Ct));
    }

    /// <summary>只通过了密码（待完成 Cookie）的人不能主动改密码，也不能改绑 TOTP（否则可以绕过二次验证）</summary>
    [Fact]
    public async Task PendingSignIn_CannotChangePasswordOrRebindTotp()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Active, mustChangePassword: false, totpBound: true);
        var originalSecret = user.TotpSecret;
        var service = Create(db);

        Assert.False((await service.ChangePasswordAsync(user.Id, null, NewPassword, signedIn: false, Client, Ct)).Success);
        Assert.False((await service.ConfirmTotpSetupAsync(user.Id, _protector.Encrypt(RfcSecret), RfcCode, signedIn: false, Client, Ct)).Success);

        var saved = await db.SysUsers.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct);
        Assert.True(_hasher.Verify(InitialPassword, saved.PasswordHash));
        Assert.Equal(originalSecret, saved.TotpSecret);
    }

    /// <summary>同一个验证码只能用一次（防重放）</summary>
    [Fact]
    public async Task VerifyTotp_SucceedsOnce_ThenRejectsReplay()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Active, mustChangePassword: false, totpBound: true);
        var service = Create(db);

        var first = await service.VerifyTotpAsync(user.Id, RfcCode, Client, Ct);
        var replay = await service.VerifyTotpAsync(user.Id, RfcCode, Client, Ct);

        Assert.Equal(SignInStep.Completed, first.Next);
        Assert.False(replay.Success);
        var logs = await LogsAsync(db, user.Username);
        Assert.Equal([true, false], logs.Select(l => l.IsSuccess));
        Assert.All(logs, log => Assert.Equal(LoginStage.Totp, log.Stage));
    }

    /// <summary>开发环境跳过验证码（sys 设计 §5.1）：照常完成登录，登录日志记为"跳过 TOTP"</summary>
    [Fact]
    public async Task SkipTotp_CompletesSignInAndLogsTheSkippedStage()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Active, mustChangePassword: false, totpBound: true);
        var service = Create(db);
        await service.PasswordSignInAsync(user.Username, WrongPassword, Client, Ct);

        var result = await service.SkipTotpAsync(user.Id, Client, Ct);

        Assert.Equal(SignInStep.Completed, result.Next);
        var saved = await ReloadAsync(db, user);
        Assert.Equal((0, _time.GetUtcNow().UtcDateTime), (saved.LoginFailCount, saved.LastLoginAt));
        var log = (await LogsAsync(db, user.Username)).Last();
        Assert.Equal((LoginStage.TotpSkipped, true), (log.Stage, log.IsSuccess));
    }

    /// <summary>只跳过验证码：还要改密码或尚未绑定 TOTP 的账号不能跳过</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task SkipTotp_RejectsAccountsNotAtTheLastStep(bool mustChangePassword, bool totpBound)
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Pending, mustChangePassword, totpBound);

        var result = await Create(db).SkipTotpAsync(user.Id, Client, Ct);

        Assert.False(result.Success);
        Assert.Null((await ReloadAsync(db, user)).LastLoginAt);
    }

    /// <summary>Redis 不可用时无法确认验证码是否用过，拒绝验证</summary>
    [Fact]
    public async Task VerifyTotp_RejectsWhenRedisUnavailable()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Active, mustChangePassword: false, totpBound: true);

        var result = await Create(db, new FakeRedisCache { Unavailable = true }).VerifyTotpAsync(user.Id, RfcCode, Client, Ct);

        Assert.False(result.Success);
        Assert.Null((await db.SysUsers.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct)).LastLoginAt);
        Assert.False(Assert.Single(await LogsAsync(db, user.Username)).IsSuccess);
    }

    /// <summary>完成标准（JAN-72，FR-028 #4）：连续 5 次失败后锁定 15 分钟，锁定期间密码正确也无法登录，管理员解锁后可立即登录</summary>
    [Fact]
    public async Task FiveFailures_LockAccount_UntilAdminUnlocks()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Active, mustChangePassword: false, totpBound: true);
        var service = Create(db);

        for (var i = 0; i < 5; i++)
        {
            Assert.False((await service.PasswordSignInAsync(user.Username, WrongPassword, Client, Ct)).Success);
        }

        var whileLocked = await service.PasswordSignInAsync(user.Username, InitialPassword, Client, Ct);
        var locked = await ReloadAsync(db, user);
        var unlock = await UserManage(db).UnlockAsync(user.Id, Ct);
        var afterUnlock = await service.PasswordSignInAsync(user.Username, InitialPassword, Client, Ct);

        Assert.Equal((5, _time.UtcNow.UtcDateTime.AddMinutes(15)), (locked.LoginFailCount, locked.LockedUntil));
        Assert.False(whileLocked.Success);
        Assert.True(unlock.Success);
        Assert.Equal(SignInStep.VerifyTotp, afterUnlock.Next);
    }

    [Fact]
    public async Task Lock_ExpiresAfterLockDuration()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Active, mustChangePassword: false, totpBound: true);
        var service = Create(db);
        for (var i = 0; i < 5; i++)
        {
            await service.PasswordSignInAsync(user.Username, WrongPassword, Client, Ct);
        }

        _time.UtcNow += TimeSpan.FromMinutes(15);

        Assert.Equal(SignInStep.VerifyTotp, (await service.PasswordSignInAsync(user.Username, InitialPassword, Client, Ct)).Next);
    }

    /// <summary>密码错误和 TOTP 错误共用一个计数；锁定后正确的验证码也不接受</summary>
    [Fact]
    public async Task TotpFailures_ShareTheCounterWithPassword()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Active, mustChangePassword: false, totpBound: true);
        var service = Create(db);
        for (var i = 0; i < 4; i++)
        {
            await service.PasswordSignInAsync(user.Username, WrongPassword, Client, Ct);
        }

        var wrongCode = await service.VerifyTotpAsync(user.Id, "000000", Client, Ct);
        var rightCodeWhileLocked = await service.VerifyTotpAsync(user.Id, RfcCode, Client, Ct);

        Assert.False(wrongCode.Success);
        Assert.False(rightCodeWhileLocked.Success);
        Assert.NotNull((await ReloadAsync(db, user)).LockedUntil);
    }

    /// <summary>登录成功时失败次数清零</summary>
    [Fact]
    public async Task SuccessfulSignIn_ResetsFailureCount()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await AddUserAsync(db, UserStatus.Active, mustChangePassword: false, totpBound: true);
        var service = Create(db);
        await service.PasswordSignInAsync(user.Username, WrongPassword, Client, Ct);
        await service.PasswordSignInAsync(user.Username, WrongPassword, Client, Ct);
        Assert.Equal(2, (await ReloadAsync(db, user)).LoginFailCount);

        await service.VerifyTotpAsync(user.Id, RfcCode, Client, Ct);

        Assert.Equal(0, (await ReloadAsync(db, user)).LoginFailCount);
    }

    private AuthService Create(SuanpanDbContext db, IRedisCache? redis = null) => new(
        db,
        _hasher,
        new TotpService(_time),
        _protector,
        new TotpReplayGuard(redis ?? new FakeRedisCache(), NullLogger<TotpReplayGuard>.Instance),
        new LoginLogService(db, _time),
        new SecurityStampValidator(db, _cache),
        new PermissionService(db, _cache),
        new ConfigService(db, _cache, NullLogger<ConfigService>.Instance),
        _time);

    private UserManageService UserManage(SuanpanDbContext db) => new(
        db, _hasher, new PermissionService(db, _cache), new SecurityStampValidator(db, _cache), new StubCurrentUser(1, "admin"));

    private static Task<SysUser> ReloadAsync(SuanpanDbContext db, SysUser user)
        => db.SysUsers.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct);

    private async Task<SysUser> AddUserAsync(SuanpanDbContext db, UserStatus status, bool mustChangePassword, bool totpBound)
    {
        var user = await TestData.AddUserAsync(db, Ct, status, TestData.AdminRoleId);
        user.PasswordHash = _hasher.Hash(InitialPassword);
        user.MustChangePassword = mustChangePassword;
        user.TotpSecret = totpBound ? _protector.Encrypt(RfcSecret) : null;
        await db.SaveChangesAsync(Ct);
        return user;
    }

    private static Task<List<SysLoginLog>> LogsAsync(SuanpanDbContext db, params string[] usernames)
        => db.SysLoginLogs.AsNoTracking().Where(l => usernames.Contains(l.Username)).OrderBy(l => l.Id).ToListAsync(Ct);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
