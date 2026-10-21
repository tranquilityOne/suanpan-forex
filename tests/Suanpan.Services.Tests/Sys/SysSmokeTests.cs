using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Common.Alerts;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Auth.Models;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Services.Sys.Security;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;
using Xunit;

namespace Suanpan.Services.Tests.Sys;

/// <summary>
/// 冒烟测试（JAN-65，ENG-002 完成标准 3）：在真实数据库上把"管理员建号 → 登录（密码 + TOTP）→ 权限判断 → 写操作留下操作日志"
/// 串成一条流程走一遍，确认迁移过来的各部分接起来是通的。各环节的分支在各自的测试类里测全，这里只走主路径。
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class SysSmokeTests(PostgresFixture postgres)
{
    // RFC 6238 附录 B 的 SHA1 测试密钥（Base32）及其在两个时刻的验证码（8 位验证码的后 6 位）
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    private static readonly (DateTimeOffset At, string Code) Activation = (DateTimeOffset.FromUnixTimeSeconds(59), "287082");
    private static readonly (DateTimeOffset At, string Code) NextSignIn = (DateTimeOffset.FromUnixTimeSeconds(1111111109), "081804");

    private const string InitialPassword = "Initial-pass-2026";
    private const string NewPassword = "Brand-new-pass-2026";

    private static readonly LoginClient Client = new("203.0.113.5", "smoke-test");

    private readonly FixedTimeProvider _time = new(Activation.At);
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly AesSecretProtector _protector = new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) })
        .Build());

    [Fact]
    public async Task NewAccount_SignsIn_IsAuthorizedByRole_AndItsWriteIsOpLogged()
    {
        await using var db = await postgres.CreateAppContextAsync(_time);
        var permissions = new PermissionService(db, _cache);
        var grantedCode = TestData.Unique("smoke") + ":user:edit";
        var otherCode = TestData.Unique("smoke") + ":user:delete";
        var role = await TestData.AddRoleAsync(db, Ct);
        var granted = await TestData.AddPermissionAsync(db, Ct, grantedCode);
        await TestData.AddPermissionAsync(db, Ct, otherCode);
        await TestData.GrantAsync(db, Ct, role.Id, granted.Id);

        // 管理员建号：待激活
        var username = TestData.Unique("smoke");
        var userManage = new UserManageService(
            db, _hasher, permissions, new SecurityStampValidator(db, _cache), new StubCurrentUser(1, "admin"));
        Assert.True((await userManage.CreateAsync(new UserCreateInput(username, "冒烟测试", null, InitialPassword, [role.Id]), Ct)).Success);
        var userId = (await db.SysUsers.AsNoTracking().SingleAsync(u => u.Username == username, Ct)).Id;

        // 首次登录：密码 → 改密码 → 绑定 TOTP，账号变为启用
        var auth = CreateAuthService(db, permissions);
        Assert.Equal(SignInStep.ChangePassword, (await auth.PasswordSignInAsync(username, InitialPassword, Client, Ct)).Next);
        Assert.Equal(SignInStep.SetupTotp, (await auth.ChangePasswordAsync(userId, null, NewPassword, signedIn: false, Client, Ct)).Next);
        Assert.True((await auth.ConfirmTotpSetupAsync(userId, _protector.Encrypt(RfcSecret), Activation.Code, signedIn: false, Client, Ct)).Success);

        // 之后的登录：密码 → TOTP
        _time.UtcNow = NextSignIn.At;
        Assert.Equal(SignInStep.VerifyTotp, (await auth.PasswordSignInAsync(username, NewPassword, Client, Ct)).Next);
        Assert.True((await auth.VerifyTotpAsync(userId, NextSignIn.Code, Client, Ct)).Success);

        // 权限判断：只放行角色授予的权限码
        var handler = new PermissionAuthorizationHandler(permissions);
        Assert.True(await IsAllowedAsync(handler, userId, grantedCode));
        Assert.False(await IsAllowedAsync(handler, userId, otherCode));

        // 写操作：经过操作日志过滤器后，sys_op_log 中有这次操作，带当时的角色和权限码
        await RunWriteActionAsync(db, permissions, userId, username, grantedCode);
        var log = await db.SysOpLogs.AsNoTracking().SingleAsync(l => l.Username == username, Ct);
        Assert.Equal((userId, role.RoleCode, grantedCode, true), (log.UserId, log.Roles, log.PermCode, log.IsSuccess));
    }

    private AuthService CreateAuthService(SuanpanDbContext db, PermissionService permissions) => new(
        db,
        _hasher,
        new TotpService(_time),
        _protector,
        new TotpReplayGuard(new FakeRedisCache(), NullLogger<TotpReplayGuard>.Instance),
        new LoginLogService(db, _time),
        new SecurityStampValidator(db, _cache),
        permissions,
        new ConfigService(db, _cache, NullLogger<ConfigService>.Instance),
        _time);

    private static async Task<bool> IsAllowedAsync(PermissionAuthorizationHandler handler, long userId, string permCode)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "smoke"));
        var context = new AuthorizationHandlerContext([new PermissionRequirement(permCode)], user, null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    /// <summary>模拟 Crm 执行一个标了 [OpLog] 的写操作（过滤器由 Crm 全局注册，这里直接调用）</summary>
    private async Task RunWriteActionAsync(SuanpanDbContext db, PermissionService permissions, long userId, string username, string permCode)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/Sys/User/Edit";
        var descriptor = new ControllerActionDescriptor
        {
            EndpointMetadata = [new OpLogAttribute("系统管理", "编辑账号"), new HasPermissionAttribute(permCode)],
        };
        var actionContext = new ActionContext(http, new RouteData(), descriptor);
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?> { ["id"] = userId }, controller: new object());
        var filter = new OpLogActionFilter(
            new OpLogService(db, _time), permissions, new StubCurrentUser(userId, username),
            new LoggingAlertService(NullLogger<LoggingAlertService>.Instance), NullLogger<OpLogActionFilter>.Instance);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(new ActionExecutedContext(actionContext, [], new object())));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
