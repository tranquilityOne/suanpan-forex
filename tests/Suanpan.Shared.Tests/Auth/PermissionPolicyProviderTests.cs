using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using Suanpan.Shared.Auth;
using Xunit;

namespace Suanpan.Shared.Tests.Auth;

public class PermissionPolicyProviderTests
{
    private readonly PermissionPolicyProvider _provider = new(Options.Create(new AuthorizationOptions()));

    [Fact]
    public async Task PermPolicy_RequiresAuthenticatedUserAndPermCode()
    {
        var policyName = new HasPermissionAttribute("sys:user:edit").Policy!;

        var policy = await _provider.GetPolicyAsync(policyName);

        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        var requirement = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
        Assert.Equal("sys:user:edit", requirement.PermCode);
    }

    [Fact]
    public async Task OtherPolicyNames_FallBackToDefaultProvider()
    {
        Assert.Null(await _provider.GetPolicyAsync("not-registered"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void HasPermission_RejectsEmptyPermCode(string permCode)
    {
        Assert.ThrowsAny<ArgumentException>(() => new HasPermissionAttribute(permCode));
    }
}
