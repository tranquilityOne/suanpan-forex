using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Sys;

public sealed class UserListViewModel
{
    public string? Keyword { get; init; }

    public UserStatus? Status { get; init; }

    public required PagedResult<UserRow> Result { get; init; }

    /// <summary>当前 UTC 时间，用于判断是否锁定、剩余多久</summary>
    public required DateTime Now { get; init; }
}
