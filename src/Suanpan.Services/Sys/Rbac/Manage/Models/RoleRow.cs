namespace Suanpan.Services.Sys.Rbac.Manage.Models;

/// <summary>角色列表的一行；IsPreset 表示可以"恢复默认授权"</summary>
public sealed record RoleRow(
    long Id,
    string RoleCode,
    string RoleName,
    bool IsBuiltin,
    bool IsPreset,
    bool IsEnabled,
    string? Remark,
    int UserCount,
    int PermCount);
