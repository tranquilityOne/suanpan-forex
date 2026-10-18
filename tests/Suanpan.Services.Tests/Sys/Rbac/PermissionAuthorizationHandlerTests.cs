using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Shared.Auth;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Rbac;

public class PermissionAuthorizationHandlerTests
{
    [Theory]
    [InlineData("sys:user:index", true)]
    [InlineData("sys:user:edit", false)]
    public async Task Succeeds_OnlyWhenSnapshotHasCode(string requiredCode, bool expected)
    {
        var snapshot = new PermissionSnapshot(false, ["viewer"], new HashSet<string> { "sys:user:index" });

        Assert.Equal(expected, await AuthorizeAsync(Authenticated(42), requiredCode, snapshot));
    }

    [Fact]
    public async Task Admin_AlwaysSucceeds()
    {
        var snapshot = new PermissionSnapshot(true, ["super_admin"], new HashSet<string>());

        Assert.True(await AuthorizeAsync(Authenticated(42), "sys:user:edit", snapshot));
    }

    [Fact]
    public async Task Anonymous_DoesNotSucceed()
    {
        var snapshot = new PermissionSnapshot(true, ["super_admin"], new HashSet<string>());

        Assert.False(await AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), "sys:user:index", snapshot));
    }

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user, string code, PermissionSnapshot snapshot)
    {
        var requirement = new PermissionRequirement(code);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        await new PermissionAuthorizationHandler(new StubPermissionService(snapshot)).HandleAsync(context);
        return context.HasSucceeded;
    }

    private static ClaimsPrincipal Authenticated(long userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], AuthSchemes.Crm));

    private sealed class StubPermissionService(PermissionSnapshot snapshot) : IPermissionService
    {
        public Task<PermissionSnapshot> GetSnapshotAsync(long userId, CancellationToken ct = default) => Task.FromResult(snapshot);

        public void Invalidate(long userId)
        {
        }

        public Task InvalidateRoleHoldersAsync(long roleId, CancellationToken ct = default) => Task.CompletedTask;

        public void InvalidateAll()
        {
        }
    }
}
