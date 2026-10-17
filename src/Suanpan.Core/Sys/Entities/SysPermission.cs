using Suanpan.Core.Common.Auditing;
using Suanpan.Core.Sys.Enums;

namespace Suanpan.Core.Sys.Entities;

/// <summary>
/// 权限点（sys_permission），同时也是菜单。由"从代码重建"生成，重建时保留已有 id，
/// 代码中已不存在的权限点停用、不删除（sys 设计 §6.3）。
/// </summary>
public class SysPermission : AuditedEntity
{
    public long Id { get; set; }

    /// <summary>父权限点 id，根为 0</summary>
    public long ParentId { get; set; }

    public PermType PermType { get; set; }

    /// <summary>权限码 {area}:{controller}:{action}，唯一，由代码自动生成</summary>
    public string PermCode { get; set; } = string.Empty;

    /// <summary>显示名（菜单标题、按钮说明）</summary>
    public string PermName { get; set; } = string.Empty;

    /// <summary>菜单路由，如 /Calendar/Event；目录和按钮为空</summary>
    public string? MenuUrl { get; set; }

    /// <summary>Tabler Icons 名称</summary>
    public string? Icon { get; set; }

    public int Sort { get; set; }

    public bool IsEnabled { get; set; } = true;
}
