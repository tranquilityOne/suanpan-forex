using Microsoft.AspNetCore.Razor.TagHelpers;
using Suanpan.Crm.Auth;
using Suanpan.Crm.TagHelpers;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Shared.Auth;
using Xunit;

namespace Suanpan.Crm.Tests.TagHelpers;

/// <summary>完成标准（JAN-73）：有权限、无权限、管理员三种情况</summary>
public class PermissionTagHelperTests
{
    private const string EditCode = "sys:user:edit";

    [Fact]
    public async Task WithPermission_KeepsElement()
    {
        var permissions = new FakePermissionService();
        permissions.Grant(1, EditCode);

        var output = await RenderAsync(permissions, userId: 1);

        Assert.Equal("a", output.TagName);
        Assert.Equal("编辑", output.Content.GetContent());
    }

    [Fact]
    public async Task WithoutPermission_SuppressesElement()
    {
        var permissions = new FakePermissionService();
        permissions.Grant(1, "sys:user:index");

        var output = await RenderAsync(permissions, userId: 1);

        Assert.Null(output.TagName);
        Assert.Empty(output.Content.GetContent());
    }

    [Fact]
    public async Task SuperAdmin_AlwaysKeepsElement()
    {
        var permissions = new FakePermissionService();
        permissions.GrantSuperAdmin(1);

        var output = await RenderAsync(permissions, userId: 1);

        Assert.Equal("a", output.TagName);
    }

    private static async Task<TagHelperOutput> RenderAsync(FakePermissionService permissions, long userId)
    {
        var helper = new PermissionTagHelper(new CurrentPermissions(new StubCurrentUser(userId), permissions)) { PermCode = EditCode };
        var context = new TagHelperContext([], new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput("a", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        output.Content.SetHtmlContent("编辑");
        await helper.ProcessAsync(context, output);
        return output;
    }

    private sealed class StubCurrentUser(long userId) : ICurrentUser
    {
        public long? UserId => userId;

        public string? UserName => $"user{userId}";
    }
}
