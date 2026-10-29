using Microsoft.AspNetCore.Mvc.Rendering;

namespace Suanpan.Crm.Models.Reference;

/// <summary>映射表单的下拉框选项，在 Controller 中准备</summary>
public sealed class IndicatorSourceFormOptions
{
    /// <summary>新增时：启用的指标；编辑时只显示当前指标</summary>
    public IReadOnlyList<SelectListItem> Indicators { get; init; } = [];

    /// <summary>新增时：两种用途；编辑时用途只读，不使用</summary>
    public IReadOnlyList<SelectListItem> Purposes { get; init; } = [];

    /// <summary>全部数据源，停用的标出来：可以保存，但数据源启用前不会从它采集（ERR-IND-003）</summary>
    public IReadOnlyList<SelectListItem> Sources { get; init; } = [];

    /// <summary>解析器目录，按适配器分组；编辑时另加映射当前的解析器（即使已不在目录中）</summary>
    public IReadOnlyList<SelectListItem> Parsers { get; init; } = [];
}
