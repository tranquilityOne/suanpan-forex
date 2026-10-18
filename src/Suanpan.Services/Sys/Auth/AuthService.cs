using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Core.Sys.Rules;
using Suanpan.Data;
using Suanpan.Services.Sys.Auth.Models;
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

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            await LogAsync(username, user.Id, LoginStage.Password, "密码错误", client, ct);
            return AuthResult.Fail(WrongCredentials);
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

        if (TryUnprotect(protectedSecret) is not { } secret)
        {
            return AuthResult.Fail("绑定信息已失效，请刷新页面重新扫码");
        }

        if (await CheckCodeAsync(user, secret, code, signedIn ? null : LoginStage.Activation, client, ct) is { } codeError)
        {
            return AuthResult.Fail(codeError);
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

        // 库里的密文无法解密属于数据损坏，直接抛出
        var secret = secretProtector.Decrypt(user.TotpSecret!);
        if (await CheckCodeAsync(user, secret, code, LoginStage.Totp, client, ct) is { } codeError)
        {
            return AuthResult.Fail(codeError);
        }

        RecordSignIn(user, client);
        await db.SaveChangesAsync(ct);
        await LogAsync(user.Username, user.Id, LoginStage.Totp, null, client, ct);
        return AuthResult.Ok(user, SignInStep.Completed);
    }

    public Task LogoutAsync(long userId, string username, LoginClient client, CancellationToken ct = default)
        => LogAsync(username, userId, LoginStage.Logout, null, client, ct);

    /// <summary>校验验证码并防重放；通过返回 null，否则返回页面提示。logStage 为空时不写登录日志（已登录后的重新绑定）</summary>
    private async Task<string?> CheckCodeAsync(
        SysUser user, string secret, string code, LoginStage? logStage, LoginClient client, CancellationToken ct)
    {
        string? failure = null;
        string? message = null;
        if (!totpService.VerifyCode(secret, code.Trim(), out var timeStep))
        {
            (failure, message) = ("TOTP 错误", WrongCode);
        }
        else
        {
            switch (await replayGuard.TryUseAsync(user.Id, timeStep))
            {
                case null:
                    (failure, message) = ("防重放检查失败（Redis 不可用）", ReplayCheckUnavailable);
                    break;
                case false:
                    (failure, message) = ("TOTP 重放", WrongCode);
                    break;
            }
        }

        if (failure is not null && logStage is { } stage)
        {
            await LogAsync(user.Username, user.Id, stage, failure, client, ct);
        }

        return message;
    }

    private Task<SysUser?> FindUsableAsync(long userId, CancellationToken ct)
        => db.SysUsers.FirstOrDefaultAsync(u => u.Id == userId && u.Status != UserStatus.Disabled, ct);

    private static SignInStep NextStep(SysUser user) => SignInStepRule.AfterPassword(user.MustChangePassword, user.TotpSecret is not null);

    private void RecordSignIn(SysUser user, LoginClient client)
    {
        user.LastLoginAt = timeProvider.GetUtcNow().UtcDateTime;
        user.LastLoginIp = client.Ip;
    }

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
