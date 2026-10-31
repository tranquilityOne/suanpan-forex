using Suanpan.Services.Calendar.Conflicts.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Calendar;

/// <summary>锁定冲突列表（日历设计 §9.5）：待处理 / 已处理两个页签</summary>
public sealed class ConflictListViewModel
{
    public bool Resolved { get; init; }

    public required PagedResult<LockConflictRow> Result { get; init; }
}
