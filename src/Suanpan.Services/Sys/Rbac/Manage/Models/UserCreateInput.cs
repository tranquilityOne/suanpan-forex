namespace Suanpan.Services.Sys.Rbac.Manage.Models;

/// <summary>新建账号：状态为待激活，初始密码线下告知本人（sys 设计 §13 第 3 条）</summary>
public sealed record UserCreateInput(string Username, string RealName, string? Email, string Password, IReadOnlyCollection<long> RoleIds);
