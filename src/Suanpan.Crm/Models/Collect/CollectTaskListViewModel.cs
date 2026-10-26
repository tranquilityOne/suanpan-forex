using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Services.Collect.Tasks.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Collect;

public sealed class CollectTaskListViewModel
{
    public required CollectTaskFilter Filter { get; init; }

    public required PagedResult<CollectTaskRow> Result { get; init; }

    /// <summary>筛选下拉框的选项：全部数据源（含停用的）、目标数据类型</summary>
    public required IReadOnlyList<SelectListItem> SourceOptions { get; init; }

    public required IReadOnlyList<SelectListItem> TargetTypeOptions { get; init; }

    /// <summary>解析器目录为空时（M1）不能新增任务，页面上给出提示（采集设计 §5.2）</summary>
    public required bool HasParsers { get; init; }
}
