using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Services.Collect.Runs.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Collect;

public sealed class CollectRunListViewModel
{
    public required CollectRunFilter Filter { get; init; }

    public required PagedResult<CollectRunRow> Result { get; init; }

    /// <summary>筛选下拉框的选项：全部任务和数据源（含停用的）、状态、触发方式</summary>
    public required IReadOnlyList<SelectListItem> TaskOptions { get; init; }

    public required IReadOnlyList<SelectListItem> SourceOptions { get; init; }

    public required IReadOnlyList<SelectListItem> StatusOptions { get; init; }

    public required IReadOnlyList<SelectListItem> TriggerOptions { get; init; }
}
