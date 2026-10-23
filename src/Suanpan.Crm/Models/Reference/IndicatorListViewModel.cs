using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Services.Reference.Indicators.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Reference;

public sealed class IndicatorListViewModel
{
    public required IndicatorFilter Filter { get; init; }

    public required PagedResult<IndicatorRow> Result { get; init; }

    /// <summary>筛选下拉框的选项：全部经济体（含停用的）、类别、重要性</summary>
    public required IReadOnlyList<SelectListItem> EconomyOptions { get; init; }

    public required IReadOnlyList<SelectListItem> CategoryOptions { get; init; }

    public required IReadOnlyList<SelectListItem> ImportanceOptions { get; init; }
}
