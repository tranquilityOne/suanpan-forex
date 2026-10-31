using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Calendar.Forecasts;

/// <summary>待录入预期值的清单（日历设计 §9.4，BR-055）：默认只列高重要性的，可切换为全部重要性</summary>
public sealed class ForecastListViewModel
{
    public bool All { get; init; }

    public required PagedResult<EventRow> Result { get; init; }

    /// <summary>来源说明的常用选项（BR-054），输入框用 datalist 提示</summary>
    public required IReadOnlyList<string> NoteOptions { get; init; }
}
