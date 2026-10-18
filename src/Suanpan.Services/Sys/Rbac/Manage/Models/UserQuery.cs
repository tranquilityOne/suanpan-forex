using Suanpan.Core.Sys.Enums;

namespace Suanpan.Services.Sys.Rbac.Manage.Models;

/// <summary>账号列表的查询条件：关键字匹配登录名或姓名（不区分大小写）</summary>
public sealed record UserQuery(string? Keyword = null, UserStatus? Status = null, int Page = 1, int PageSize = 20);
