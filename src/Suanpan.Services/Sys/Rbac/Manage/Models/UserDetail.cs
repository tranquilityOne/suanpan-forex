using Suanpan.Core.Sys.Enums;

namespace Suanpan.Services.Sys.Rbac.Manage.Models;

/// <summary>编辑账号页面需要的数据</summary>
public sealed record UserDetail(long Id, string Username, string RealName, string? Email, UserStatus Status, IReadOnlyList<long> RoleIds);
