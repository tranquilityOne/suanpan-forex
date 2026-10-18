namespace Suanpan.Services.Sys.Rbac.Manage.Models;

/// <summary>编辑账号：只改姓名、邮箱、角色，登录名不可改</summary>
public sealed record UserUpdateInput(long Id, string RealName, string? Email, IReadOnlyCollection<long> RoleIds);
