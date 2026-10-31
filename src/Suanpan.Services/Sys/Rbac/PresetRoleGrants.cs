using Suanpan.Core.Sys.Constants;

namespace Suanpan.Services.Sys.Rbac;

/// <summary>
/// 预置角色的默认授权表（sys 设计 §4.5）：角色编码 → 权限码通配规则（如 calendar:*:index）。
/// 各模块的设计文档在"权限码与预置角色"一节给出本模块的规则，实现该模块时补充到这里。
/// 系统管理栏目的权限只给管理员，这里不列（sys 设计 §9.1）。
/// </summary>
public static class PresetRoleGrants
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Patterns =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            // 基础数据见基础数据设计 §6.1；数据采集见采集设计 §8.3：快照（FR-004）编辑和只读不能看，所以按页面逐个列出，不用 collect:*:index；
            // 财经日历见日历设计 §9.2：编辑按功能逐个列出，不用 calendar:*:*（预期值的"公布后修改"只给管理员）
            [RoleCodes.Engineer] =
            [
                "reference:*:index", "reference:*:detail", "reference:indicator_source:*",
                "collect:*:*",
                "calendar:*:index", "calendar:*:detail",
            ],
            [RoleCodes.Editor] =
            [
                "reference:*:index", "reference:*:detail", "reference:economy:*", "reference:currency:*", "reference:indicator:*",
                "collect:source:index", "collect:source:detail", "collect:task:index", "collect:task:trigger", "collect:run:index", "collect:run:detail",
                "calendar:*:index", "calendar:*:detail", "calendar:unconfirmed:*", "calendar:unmatched:*", "calendar:event:*", "calendar:review:*",
                "calendar:forecast:save",
            ],
            [RoleCodes.Viewer] =
            [
                "reference:*:index", "reference:*:detail",
                "collect:source:index", "collect:source:detail", "collect:task:index", "collect:run:index", "collect:run:detail",
                "calendar:*:index", "calendar:*:detail",
            ],
        };
}
