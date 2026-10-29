using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Services.Calendar.Unmatched.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Calendar;

public sealed class UnmatchedListViewModel
{
    public required UnmatchedFilter Filter { get; init; }

    public required PagedResult<UnmatchedReleaseGroup> Result { get; init; }

    /// <summary>筛选下拉框的选项：全部数据源（含停用的）</summary>
    public required IReadOnlyList<SelectListItem> SourceOptions { get; init; }
}
