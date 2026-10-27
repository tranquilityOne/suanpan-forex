using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Services.Reference.IndicatorSources.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Reference;

public sealed class IndicatorSourceListViewModel
{
    public required IndicatorSourceFilter Filter { get; init; }

    public required PagedResult<IndicatorSourceRow> Result { get; init; }

    /// <summary>筛选下拉框的选项：全部数据源（含停用的）</summary>
    public required IReadOnlyList<SelectListItem> SourceOptions { get; init; }

    /// <summary>解析器目录为空时（M1）不能新增映射，页面上给出提示</summary>
    public required bool HasParsers { get; init; }
}
