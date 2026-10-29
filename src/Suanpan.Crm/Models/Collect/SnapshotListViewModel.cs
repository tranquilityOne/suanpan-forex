using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Services.Collect.Snapshots.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Collect;

public sealed class SnapshotListViewModel
{
    public required SnapshotFilter Filter { get; init; }

    public required PagedResult<SnapshotRow> Result { get; init; }

    /// <summary>筛选下拉框的选项：全部任务和数据源（含停用的）、运行记录的状态</summary>
    public required IReadOnlyList<SelectListItem> TaskOptions { get; init; }

    public required IReadOnlyList<SelectListItem> SourceOptions { get; init; }

    public required IReadOnlyList<SelectListItem> RunStatusOptions { get; init; }
}
