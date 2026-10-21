using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Shared.Auth;
using Xunit;

namespace Suanpan.Crm.Tests.Auth;

/// <summary>
/// FR-028 验收第 3 条（BR-145）：权限在后端校验，不只是界面隐藏按钮。
/// 账号能打开系统管理的列表页，直接请求没有授权的操作时，在进入 action 之前就被拒绝，也不会留下操作日志。
/// </summary>
public class BackendAuthorizationTests
{
    private const long UserId = 7;

    [Theory]
    [InlineData("POST", "/Sys/User/SetStatus?id=1&status=Disabled")]
    [InlineData("GET", "/Sys/OpLog/Export")]
    public async Task OperationWithoutPermission_IsRejectedBeforeItRuns(string method, string path)
    {
        await using var app = new CrmApp();
        app.Permissions.Grant(UserId, "sys:user:index", "sys:op_log:index");
        var cookie = app.CreateCookie(UserId);

        var response = await app.CreateBrowser().SendAsync(method == "POST" ? Requests.Post(path, cookie) : Requests.Get(path, cookie), Ct);

        Assert.Equal("/Account/AccessDenied", response.RedirectPath());
        Assert.Empty(app.OpLogs.Written);
    }

    /// <summary>Crm 的 Area 页面全部要求权限码，没有允许匿名访问的：标了 [AllowAnonymous] 的写操作会绕过上面的校验</summary>
    [Fact]
    public async Task EveryAreaActionRequiresPermission()
    {
        await using var app = new CrmApp();

        var actions = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(d => d.ControllerTypeInfo.Assembly == typeof(Program).Assembly)
            .Where(d => d.RouteValues.TryGetValue("area", out var area) && !string.IsNullOrEmpty(area))
            .ToList();

        Assert.NotEmpty(actions);
        Assert.All(actions, d =>
        {
            Assert.True(d.EndpointMetadata.OfType<HasPermissionAttribute>().Any(), $"{d.DisplayName} 没有权限码");
            Assert.False(d.EndpointMetadata.OfType<IAllowAnonymous>().Any(), $"{d.DisplayName} 允许匿名访问");
        });
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
