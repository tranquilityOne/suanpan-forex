namespace Suanpan.Services.Sys.Rbac.Manage.Models;

/// <summary>修改权限点：只允许改图标、排序、启停，其余以代码为准（sys 设计 §9.1）</summary>
public sealed record PermissionEditInput(long Id, string? Icon, int Sort, bool IsEnabled);
