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
            [RoleCodes.Engineer] = [],
            [RoleCodes.Editor] = [],
            [RoleCodes.Viewer] = [],
        };
}
