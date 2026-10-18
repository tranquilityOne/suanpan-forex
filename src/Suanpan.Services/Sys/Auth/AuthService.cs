using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Core.Sys.Rules;
using Suanpan.Data;
using Suanpan.Services.Sys.Auth.Models;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Security;

namespace Suanpan.Services.Sys.Auth;

public sealed class AuthService(
    SuanpanDbContext db,
    IPasswordHasher passwordHasher,
    ITotpService totpService,
    ISecretProtector secretProtector,
    TotpReplayGuard replayGuard,
    ILoginLogService loginLog,
    ISecurityStampValidator stampValidator,
    IPermissionService permissionService,
    IConfigService configService,
    TimeProvider timeProvider) : IAuthService
{
    /// <summary>验证器 App 中显示的发行方</summary>
    public const string TotpIssuer = "数据中台";

    // 页面提示：账号不存在和密码错误统一提示，防止探测账号（ERR-AUTH-001）
    private const string WrongCredentials = "用户名或密码错误";
    private const string WrongCode = "验证码错误";
    private const string ReplayCheckUnavailable = "暂时无法校验验证码，请稍后重试";
    private const string AccountUnavailable = "账号不存在或已停用";
    private const string StepNotAllowed = "请按登录步骤操作";

    private const int MaxFailureWriteAttempts = 10;

    public async Task<AuthResult> PasswordSignInAsync(string username, string password, LoginClient client, CancellationToken ct = default)
    {
        username = username.Trim();
        var user = await db.SysUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null)
        {
            await LogAsync(username, null, LoginStage.Password, "账号不存在", client, ct);
            return AuthResult.Fail(WrongCredentials);
        }

        if (user.Status == UserStatus.Disabled)
        {
            await LogAsync(username, user.Id, LoginStage.Password, "账号已停用", client, ct);
            return AuthResult.Fail("账号已停用");
        }

        // 锁定期间密码正确也拒绝，不再计数
        if (await RejectIfLockedAsync(user, LoginStage.Password, client, ct) is { } locked)
        {
            return locked;
        }

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            return await FailAndCountAsync(user, LoginStage.Password, "密码错误", WrongCredentials, client, ct);
        }

        await LogAsync(username, user.Id, LoginStage.Password, null, client, ct);
        return AuthResult.Ok(user, NextStep(user));
    }

    public async Task<AuthResult> GetProgressAsync(long userId, CancellationToken ct = default)
    {
        var user = await FindUsableAsync(userId, ct);
        return user is null ? AuthResult.Fail(AccountUnavailable) : AuthResult.Ok(user, NextStep(user));
    }

    public async Task<AuthResult> ChangePasswordAsync(
        long userId, string? currentPassword, string newPassword, bool signedIn, LoginClient client, CancellationToken ct = default)
    {
        var user = await FindUsableAsync(userId, ct);
        if (user is null)
        {
            return AuthResult.Fail(AccountUnavailable);
        }

        if (signedIn)
        {
            if (currentPassword is null || !passwordHasher.Verify(currentPassword, user.PasswordHash))
            {
                return AuthResult.Fail("当前密码不正确");
            }
        }
        else if (!user.MustChangePassword)
        {
            return AuthResult.Fail(StepNotAllowed);
        }

        if (PasswordPolicy.Validate(newPassword) is { } policyError)
        {
            return AuthResult.Fail(policyError);
        }

        if (passwordHasher.Verify(newPassword, user.PasswordHash))
        {
            return AuthResult.Fail("新密码不能与旧密码相同");
        }

        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.SecurityStamp = NewSecurityStamp();
        await db.SaveChangesAsync(ct);
        stampValidator.Invalidate(user.Id);

        if (!signedIn)
        {
            await LogAsync(user.Username, user.Id, LoginStage.Activation, null, client, ct);
        }

        return AuthResult.Ok(user, signedIn ? SignInStep.Completed : NextStep(user));
    }

    public TotpSetup CreateTotpSetup(string username) => BuildSetup(username, totpService.GenerateSecret());

    public TotpSetup? RestoreTotpSetup(string protectedSecret, string username)
        => TryUnprotect(protectedSecret) is { } secret ? BuildSetup(username, secret) : null;

    public async Task<AuthResult> ConfirmTotpSetupAsync(
        long userId, string protectedSecret, string code, bool signedIn, LoginClient client, CancellationToken ct = default)
    {
        var user = await FindUsableAsync(userId, ct);
        if (user is null)
        {
            return AuthResult.Fail(AccountUnavailable);
        }

        // 只通过了密码的人只能在尚未绑定时绑定，不能改绑（否则可以绕过二次验证）
        if (!signedIn && NextStep(user) != SignInStep.SetupTotp)
        {
            return AuthResult.Fail(StepNotAllowed);
        }

        if (!signedIn && await RejectIfLockedAsync(user, LoginStage.Activation, client, ct) is { } locked)
        {
            return locked;
        }

        if (TryUnprotect(protectedSecret) is not { } secret)
        {
            return AuthResult.Fail("绑定信息已失效，请刷新页面重新扫码");
        }

        if (await CheckCodeAsync(user, secret, code, signedIn ? null : LoginStage.Activation, client, ct) is { } codeFailure)
        {
            return codeFailure;
        }

        var activated = user.Status == UserStatus.Pending;
        user.TotpSecret = secretProtector.Encrypt(secret);
        user.SecurityStamp = NewSecurityStamp();
        if (activated)
        {
            user.Status = UserStatus.Active;
        }

        if (!signedIn)
        {
            RecordSignIn(user, client);
        }

        await db.SaveChangesAsync(ct);
        stampValidator.Invalidate(user.Id);
        if (activated)
        {
            // 待激活账号的权限快照为空，激活后要重新生成
            permissionService.Invalidate(user.Id);
        }

        if (!signedIn)
        {
            await LogAsync(user.Username, user.Id, LoginStage.Activation, null, client, ct);
        }

        return AuthResult.Ok(user, SignInStep.Completed);
    }

    public async Task<AuthResult> VerifyTotpAsync(long userId, string code, LoginClient client, CancellationToken ct = default)
    {
        var user = await FindUsableAsync(userId, ct);
        if (user is null)
        {
            return AuthResult.Fail(AccountUnavailable);
        }

        if (NextStep(user) != SignInStep.VerifyTotp)
        {
            return AuthResult.Fail(StepNotAllowed);
        }

        if (await RejectIfLockedAsync(user, LoginStage.Totp, client, ct) is { } locked)
        {
            return locked;
        }

        // 库里的密文无法解密属于数据损坏，直接抛出
        var secret = secretProtector.Decrypt(user.TotpSecret!);
        if (await CheckCodeAsync(user, secret, code, LoginStage.Totp, client, ct) is { } codeFailure)
        {
            return codeFailure;
        }

        RecordSignIn(user, client);
        await db.SaveChangesAsync(ct);
        await LogAsync(user.Username, user.Id, LoginStage.Totp, null, client, ct);
        return AuthResult.Ok(user, SignInStep.Completed);
    }

    public Task LogoutAsync(long userId, string username, LoginClient client, CancellationToken ct = default)
        => LogAsync(username, userId, LoginStage.Logout, null, client, ct);

    /// <summary>
    /// 校验验证码并防重放；通过返回 null，否则返回失败结果。loginStage 为空表示不在登录过程中（已登录后的重新绑定），
    /// 此时不写登录日志、不计失败次数。验证码错误和重放计入失败次数；Redis 不可用不计（不是用户的问题）。
    /// </summary>
    private async Task<AuthResult?> CheckCodeAsync(
        SysUser user, string secret, string code, LoginStage? loginStage, LoginClient client, CancellationToken ct)
    {
        string? failure = null;
        if (!totpService.VerifyCode(secret, code.Trim(), out var timeStep))
        {
            failure = "TOTP 错误";
        }
        else
        {
            switch (await replayGuard.TryUseAsync(user.Id, timeStep))
            {
                case null:
                    if (loginStage is { } stage)
                    {
                        await LogAsync(user.Username, user.Id, stage, "防重放检查失败（Redis 不可用）", client, ct);
                    }

                    return AuthResult.Fail(ReplayCheckUnavailable);
                case false:
                    failure = "TOTP 重放";
                    break;
            }
        }

        if (failure is null)
        {
            return null;
        }

        return loginStage is { } countedStage
            ? await FailAndCountAsync(user, countedStage, failure, WrongCode, client, ct)
            : AuthResult.Fail(WrongCode);
    }

    /// <summary>账号处于锁定期时拒绝并写登录日志，提示剩余时间；未锁定返回 null</summary>
    private async Task<AuthResult?> RejectIfLockedAsync(SysUser user, LoginStage stage, LoginClient client, CancellationToken ct)
    {
        var now = Now;
        if (!LockoutRule.IsLocked(user.LockedUntil, now))
        {
            return null;
        }

        await LogAsync(user.Username, user.Id, stage, "已锁定", client, ct);
        return AuthResult.Fail(LockedMessage(user.LockedUntil!.Value, now));
    }

    /// <summary>记一次失败并写登录日志；这次失败导致锁定时，提示改为锁定的剩余时间</summary>
    private async Task<AuthResult> FailAndCountAsync(
        SysUser user, LoginStage stage, string failure, string message, LoginClient client, CancellationToken ct)
    {
        var state = await RecordFailureAsync(user.Id, ct);
        if (state.LockedUntil is { } lockedUntil)
        {
            await LogAsync(user.Username, user.Id, stage, failure + "，账号已锁定", client, ct);
            return AuthResult.Fail(LockedMessage(lockedUntil, Now));
        }

        await LogAsync(user.Username, user.Id, stage, failure, client, ct);
        return AuthResult.Fail(message);
    }

    /// <summary>
    /// 失败次数加 1，达到上限时锁定（规则见 LockoutRule）。写入用比较并交换：只有计数和锁定时间都没被其他请求改过才写入，
    /// 否则重读再算，同一账号的并发失败不会丢计数。
    /// </summary>
    private async Task<LockoutState> RecordFailureAsync(long userId, CancellationToken ct)
    {
        var maxFailures = await configService.GetIntAsync(SysConfigKeys.LoginMaxFailures, SysConfigKeys.LoginMaxFailuresDefault, ct);
        var lockMinutes = await configService.GetIntAsync(SysConfigKeys.LoginLockMinutes, SysConfigKeys.LoginLockMinutesDefault, ct);

        for (var attempt = 0; attempt < MaxFailureWriteAttempts; attempt++)
        {
            var current = await db.SysUsers.AsNoTracking().Where(u => u.Id == userId)
                .Select(u => new { u.LoginFailCount, u.LockedUntil })
                .SingleAsync(ct);
            var next = LockoutRule.AfterFailure(current.LoginFailCount, current.LockedUntil, Now, maxFailures, TimeSpan.FromMinutes(lockMinutes));
            var failCount = next.FailCount;
            var lockedUntil = next.LockedUntil;

            var written = await db.SysUsers
                .Where(u => u.Id == userId && u.LoginFailCount == current.LoginFailCount && u.LockedUntil == current.LockedUntil)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.LoginFailCount, failCount)
                    .SetProperty(u => u.LockedUntil, lockedUntil), ct);
            if (written == 1)
            {
                // ExecuteUpdate 不经过变更跟踪：同一上下文中已跟踪的账号实体重新加载，之后的锁定判断才能看到新值
                if (db.ChangeTracker.Entries<SysUser>().FirstOrDefault(e => e.Entity.Id == userId) is { } tracked)
                {
                    await tracked.ReloadAsync(ct);
                }

                return next;
            }
        }

        throw new InvalidOperationException($"账号 {userId} 的失败次数并发更新冲突过多");
    }

    private static string LockedMessage(DateTime lockedUntil, DateTime now)
        => $"账号已锁定，请 {LockoutRule.RemainingMinutes(lockedUntil, now)} 分钟后再试";

    private Task<SysUser?> FindUsableAsync(long userId, CancellationToken ct)
        => db.SysUsers.FirstOrDefaultAsync(u => u.Id == userId && u.Status != UserStatus.Disabled, ct);

    private static SignInStep NextStep(SysUser user) => SignInStepRule.AfterPassword(user.MustChangePassword, user.TotpSecret is not null);

    /// <summary>登录完成：记录时间和 IP，失败次数清零。失败计数用 ExecuteUpdate 写入，实体上的值可能是旧的，所以强制写这两列</summary>
    private void RecordSignIn(SysUser user, LoginClient client)
    {
        user.LastLoginAt = Now;
        user.LastLoginIp = client.Ip;
        user.LoginFailCount = 0;
        user.LockedUntil = null;
        db.Entry(user).Property(u => u.LoginFailCount).IsModified = true;
        db.Entry(user).Property(u => u.LockedUntil).IsModified = true;
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private TotpSetup BuildSetup(string username, string secret)
        => new(secretProtector.Encrypt(secret), secret, totpService.BuildOtpauthUri(TotpIssuer, username, secret));

    private string? TryUnprotect(string protectedSecret)
    {
        try
        {
            return secretProtector.Decrypt(protectedSecret);
        }
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private Task LogAsync(string username, long? userId, LoginStage stage, string? failReason, LoginClient client, CancellationToken ct)
        => loginLog.AddAsync(new SysLoginLog
        {
            Username = username,
            UserId = userId,
            Stage = stage,
            IsSuccess = failReason is null,
            FailReason = failReason,
            Ip = client.Ip,
            UserAgent = client.UserAgent,
        }, ct);

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");
}
