using Microsoft.AspNetCore.Mvc.Rendering;

namespace Suanpan.Crm.Models.Collect;

/// <summary>数据源表单的下拉框选项和页面状态，在 Controller 中准备</summary>
public sealed class DataSourceFormOptions
{
    /// <summary>启用的经济体；编辑时另加数据源当前的经济体（即使已停用）</summary>
    public IReadOnlyList<SelectListItem> Economies { get; init; } = [];

    public IReadOnlyList<SelectListItem> Types { get; init; } = [];

    public IReadOnlyList<SelectListItem> RateLimitUnits { get; init; } = [];

    public IReadOnlyList<SelectListItem> ComplianceClasses { get; init; } = [];

    public IReadOnlyList<SelectListItem> RobotsResults { get; init; } = [];

    /// <summary>编辑时库里是否已有凭据：有时输入框提示"留空表示不变"，并显示"清除凭据"</summary>
    public bool HasCredential { get; init; }
}
