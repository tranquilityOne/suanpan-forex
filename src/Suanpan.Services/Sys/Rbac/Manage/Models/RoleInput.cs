namespace Suanpan.Services.Sys.Rbac.Manage.Models;

/// <summary>新建或编辑角色；编辑时不能改角色编码</summary>
public sealed record RoleInput(string RoleCode, string RoleName, bool IsEnabled, string? Remark);
