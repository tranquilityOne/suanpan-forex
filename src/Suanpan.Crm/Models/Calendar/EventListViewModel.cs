using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Calendar;

public sealed class EventListViewModel
{
    public required EventFilter Filter { get; init; }

    public required PagedResult<EventRow> Result { get; init; }

    /// <summary>筛选下拉框的选项：全部经济体（含停用的）、重要性、状态</summary>
    public required IReadOnlyList<SelectListItem> EconomyOptions { get; init; }

    public required IReadOnlyList<SelectListItem> ImportanceOptions { get; init; }

    public required IReadOnlyList<SelectListItem> StatusOptions { get; init; }
}
