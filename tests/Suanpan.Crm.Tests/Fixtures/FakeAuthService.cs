using System.Collections.Concurrent;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Auth.Models;

namespace Suanpan.Crm.Tests.Fixtures;

/// <summary>
/// 登录服务的替身：只模拟各步骤的结果和安全戳的变化，用来测试 AccountController 的 Cookie 和跳转。
/// 登录服务本身（校验、写库、写日志）在 Suanpan.Services.Tests 中连真实数据库测试。
/// </summary>
public sealed class FakeAuthService(FakeSecurityStampValidator stamps) : IAuthService
{
    public const string Password = "Right-pass-2026";
    public const string Code = "123456";

    private readonly ConcurrentDictionary<long, Account> _accounts = new();

    public ConcurrentBag<string> LoggedOut { get; } = [];

    /// <summary>添加账号；next 为密码通过后应进行的步骤</summary>
    public void AddAccount(long id, SignInStep next) => _accounts[id] = new Account(id, $"user{id}") { Next = next };

    public Task<AuthResult> PasswordSignInAsync(string username, string password, LoginClient client, CancellationToken ct = default)
    {
        var account = _accounts.Values.FirstOrDefault(a => a.Username == username);
        return Task.FromResult(account is null || password != Password ? AuthResult.Fail("用户名或密码错误") : Ok(account, account.Next));
    }

    public Task<AuthResult> GetProgressAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(Ok(_accounts[userId], _accounts[userId].Next));

    public Task<AuthResult> ChangePasswordAsync(
        long userId, string? currentPassword, string newPassword, bool signedIn, LoginClient client, CancellationToken ct = default)
    {
        var account = _accounts[userId];
        if (signedIn && currentPassword != Password)
        {
            return Task.FromResult(AuthResult.Fail("当前密码不正确"));
        }

        stamps.Renew(userId);
        if (!signedIn)
        {
            account.Next = SignInStep.SetupTotp;
        }

        return Task.FromResult(Ok(account, signedIn ? SignInStep.Completed : account.Next));
    }

    public TotpSetup CreateTotpSetup(string username) => new("protected-secret", "GEZDGNBVGY3TQOJQ", "otpauth://totp/test?secret=GEZDGNBVGY3TQOJQ");

    public TotpSetup? RestoreTotpSetup(string protectedSecret, string username) => CreateTotpSetup(username);

    public Task<AuthResult> ConfirmTotpSetupAsync(
        long userId, string protectedSecret, string code, bool signedIn, LoginClient client, CancellationToken ct = default)
    {
        if (code != Code)
        {
            return Task.FromResult(AuthResult.Fail("验证码错误"));
        }

        stamps.Renew(userId);
        _accounts[userId].Next = SignInStep.VerifyTotp;
        return Task.FromResult(Ok(_accounts[userId], SignInStep.Completed));
    }

    public Task<AuthResult> VerifyTotpAsync(long userId, string code, LoginClient client, CancellationToken ct = default)
        => Task.FromResult(code == Code ? Ok(_accounts[userId], SignInStep.Completed) : AuthResult.Fail("验证码错误"));

    public Task<AuthResult> SkipTotpAsync(long userId, LoginClient client, CancellationToken ct = default)
        => Task.FromResult(_accounts[userId].Next == SignInStep.VerifyTotp ? Ok(_accounts[userId], SignInStep.Completed) : AuthResult.Fail("请按登录步骤操作"));

    public Task LogoutAsync(long userId, string username, LoginClient client, CancellationToken ct = default)
    {
        LoggedOut.Add(username);
        return Task.CompletedTask;
    }

    private AuthResult Ok(Account account, SignInStep next) => AuthResult.Ok(
        new SysUser { Id = account.Id, Username = account.Username, RealName = "测试", SecurityStamp = stamps.Current(account.Id) }, next);

    private sealed record Account(long Id, string Username)
    {
        public SignInStep Next { get; set; }
    }
}
