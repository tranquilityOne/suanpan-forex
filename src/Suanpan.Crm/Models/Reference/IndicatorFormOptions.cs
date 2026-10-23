using Microsoft.AspNetCore.Mvc.Rendering;

namespace Suanpan.Crm.Models.Reference;

/// <summary>指标表单的下拉框选项，在 Controller 中准备</summary>
public sealed class IndicatorFormOptions
{
    /// <summary>启用的经济体（只在新增时使用：编辑时经济体只读）</summary>
    public IReadOnlyList<SelectListItem> Economies { get; init; } = [];

    /// <summary>经济体编码 → 它的货币，新增时选了经济体后，页面脚本把货币默认设为经济体的货币</summary>
    public IReadOnlyDictionary<string, string> EconomyCurrencies { get; init; } = new Dictionary<string, string>();

    /// <summary>启用的货币；编辑时另加指标当前使用的货币（即使已停用）</summary>
    public IReadOnlyList<SelectListItem> Currencies { get; init; } = [];

    public IReadOnlyList<SelectListItem> Categories { get; init; } = [];

    public IReadOnlyList<SelectListItem> ValueTypes { get; init; } = [];

    public IReadOnlyList<SelectListItem> Units { get; init; } = [];

    public IReadOnlyList<SelectListItem> Frequencies { get; init; } = [];

    public IReadOnlyList<SelectListItem> Importances { get; init; } = [];

    public IReadOnlyList<SelectListItem> Directions { get; init; } = [];
}
