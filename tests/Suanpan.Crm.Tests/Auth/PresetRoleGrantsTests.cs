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
    /// <summary>
    /// 基础数据设计 §6.1：编辑可以维护经济体、货币和指标，采集工程师可以维护指标的数据源映射（FR-006 验收、PRD §13.1.2），
    /// 其余只能查看列表和指标详情
    /// </summary>
    [Theory]
    [InlineData(RoleCodes.Editor,
        "reference:economy:index reference:economy:create reference:economy:edit reference:economy:set_status reference:economy:delete "
        + "reference:currency:index reference:currency:create reference:currency:edit reference:currency:set_status reference:currency:delete "
        + "reference:indicator:index reference:indicator:detail reference:indicator:create reference:indicator:edit "
        + "reference:indicator:set_status reference:indicator:delete reference:indicator_source:index")]
    [InlineData(RoleCodes.Engineer,
        "reference:economy:index reference:currency:index reference:indicator:index reference:indicator:detail "
        + "reference:indicator_source:index reference:indicator_source:create reference:indicator_source:edit reference:indicator_source:delete")]
    [InlineData(RoleCodes.Viewer,
        "reference:economy:index reference:currency:index reference:indicator:index reference:indicator:detail reference:indicator_source:index")]
    public async Task ReferenceGrantsMatchDesign(string roleCode, string expected)
    {
        await using var app = new CrmApp();
        var patterns = PresetRoleGrants.Patterns[roleCode];

        var granted = AreaCatalogTests.GeneratedPermCodes(app)
            .Where(code => code.StartsWith("reference:", StringComparison.Ordinal))
            .Where(code => patterns.Any(pattern => PermCodePattern.IsMatch(pattern, code)));

        Assert.Equal(expected.Split(' ').Order(StringComparer.Ordinal), granted.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// 采集设计 §8.3：采集工程师维护数据源和任务、查看快照和重新解析；编辑只能查看，另可立即执行日历排期、公布值类任务；
    /// 只读只能查看。编辑和只读都看不到快照（PRD §13.1.2）；盯盘状态全部角色都能看（FR-003）
    /// </summary>
    [Theory]
    [InlineData(RoleCodes.Engineer,
        "collect:source:index collect:source:detail collect:source:create collect:source:edit "
        + "collect:source:set_status collect:source:delete collect:source:test "
        + "collect:task:index collect:task:create collect:task:edit collect:task:set_status collect:task:delete "
        + "collect:task:trigger collect:task:trigger_any collect:run:index collect:run:detail "
        + "collect:snapshot:index collect:snapshot:detail collect:snapshot:download collect:snapshot:reparse collect:watch:index")]
    [InlineData(RoleCodes.Editor,
        "collect:source:index collect:source:detail collect:task:index collect:task:trigger collect:run:index collect:run:detail collect:watch:index")]
    [InlineData(RoleCodes.Viewer,
        "collect:source:index collect:source:detail collect:task:index collect:run:index collect:run:detail collect:watch:index")]
    public async Task CollectGrantsMatchDesign(string roleCode, string expected)
    {
        await using var app = new CrmApp();
        var patterns = PresetRoleGrants.Patterns[roleCode];

        var granted = AreaCatalogTests.GeneratedPermCodes(app)
            .Where(code => code.StartsWith("collect:", StringComparison.Ordinal))
            .Where(code => patterns.Any(pattern => PermCodePattern.IsMatch(pattern, code)));

        Assert.Equal(expected.Split(' ').Order(StringComparer.Ordinal), granted.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// 日历设计 §9.2、FR-007 的权限行：编辑处理待确认事件和未匹配条目；采集工程师、只读只能查看（JAN-91 验收第 3 条）。
    /// 日历事件的列表和详情、待复核全部角色都能看；修改数值、取消、解锁、处理待复核只给编辑（FR-009，日历设计 §9.2）。
    /// 预期值的待录入清单全部角色都能看，编辑能在公布前录入，公布后的更正只给管理员（FR-010，BR-057）；
    /// 锁定冲突全部角色都能看，只有编辑能处理（FR-011，ERR-AUTH-002）
    /// </summary>
    [Theory]
    [InlineData(RoleCodes.Editor,
        "calendar:event:index calendar:event:detail calendar:event:edit calendar:event:cancel calendar:event:unlock "
        + "calendar:review:index calendar:review:confirm calendar:review:reject calendar:forecast:index calendar:forecast:save "
        + "calendar:conflict:index calendar:conflict:keep calendar:conflict:adopt "
        + "calendar:unconfirmed:index calendar:unconfirmed:keep calendar:unconfirmed:cancel "
        + "calendar:unmatched:index calendar:unmatched:ignore calendar:unmatched:restore calendar:unmatched:rematch")]
    [InlineData(RoleCodes.Engineer,
        "calendar:event:index calendar:event:detail calendar:review:index calendar:forecast:index calendar:conflict:index "
        + "calendar:unconfirmed:index calendar:unmatched:index")]
    [InlineData(RoleCodes.Viewer,
        "calendar:event:index calendar:event:detail calendar:review:index calendar:forecast:index calendar:conflict:index "
        + "calendar:unconfirmed:index calendar:unmatched:index")]
    public async Task CalendarGrantsMatchDesign(string roleCode, string expected)
    {
        await using var app = new CrmApp();
        var patterns = PresetRoleGrants.Patterns[roleCode];

        var granted = AreaCatalogTests.GeneratedPermCodes(app)
            .Where(code => code.StartsWith("calendar:", StringComparison.Ordinal))
            .Where(code => patterns.Any(pattern => PermCodePattern.IsMatch(pattern, code)));

        Assert.Equal(expected.Split(' ').Order(StringComparer.Ordinal), granted.Order(StringComparer.Ordinal));
    }
}
