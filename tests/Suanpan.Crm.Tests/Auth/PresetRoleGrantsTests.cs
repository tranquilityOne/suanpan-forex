using Suanpan.Core.Sys.Constants;
using Suanpan.Core.Sys.Rules;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Sys.Rbac;
using Xunit;

namespace Suanpan.Crm.Tests.Auth;

/// <summary>
/// 预置角色的通配规则（sys 设计 §4.5）套在代码生成的权限码上，得到的授权与各模块设计文档一致。
/// 通配规则写错或页面改名时，"恢复默认授权"会悄悄少授或多授，只能靠这里发现。
/// </summary>
public class PresetRoleGrantsTests
{
    /// <summary>基础数据设计 §6.1：编辑可以维护经济体和货币，采集工程师和只读只能查看</summary>
    [Theory]
    [InlineData(RoleCodes.Editor,
        "reference:economy:index reference:economy:create reference:economy:edit reference:economy:set_status reference:economy:delete "
        + "reference:currency:index reference:currency:create reference:currency:edit reference:currency:set_status reference:currency:delete")]
    [InlineData(RoleCodes.Engineer, "reference:economy:index reference:currency:index")]
    [InlineData(RoleCodes.Viewer, "reference:economy:index reference:currency:index")]
    public async Task ReferenceGrantsMatchDesign(string roleCode, string expected)
    {
        await using var app = new CrmApp();
        var patterns = PresetRoleGrants.Patterns[roleCode];

        var granted = AreaCatalogTests.GeneratedPermCodes(app)
            .Where(code => code.StartsWith("reference:", StringComparison.Ordinal))
            .Where(code => patterns.Any(pattern => PermCodePattern.IsMatch(pattern, code)));

        Assert.Equal(expected.Split(' ').Order(StringComparer.Ordinal), granted.Order(StringComparer.Ordinal));
    }
}
