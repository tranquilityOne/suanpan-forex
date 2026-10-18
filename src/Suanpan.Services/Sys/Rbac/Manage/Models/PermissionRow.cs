using Suanpan.Core.Sys.Enums;

namespace Suanpan.Services.Sys.Rbac.Manage.Models;

/// <summary>权限点树形表格的一行，按树的顺序排列，Depth 用于缩进</summary>
public sealed record PermissionRow(
    long Id,
    long ParentId,
    PermType PermType,
    string PermCode,
    string PermName,
    string? MenuUrl,
    string? Icon,
    int Sort,
    bool IsEnabled,
    int Depth);
