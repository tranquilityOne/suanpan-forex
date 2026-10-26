using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Core.Collect.Enums;
using Suanpan.Services.Collect.Tasks.Models;

namespace Suanpan.Crm.Models.Collect;

/// <summary>采集任务表单的下拉框选项和页面状态，在 Controller 中准备</summary>
public sealed class CollectTaskFormOptions
{
    /// <summary>全部数据源，停用的标出来：数据源新建时为停用，可以先配好任务再启用</summary>
    public IReadOnlyList<SelectListItem> Sources { get; init; } = [];

    /// <summary>解析器目录，按适配器分组，文字中带目标数据类型；编辑时另加任务当前的解析器（即使已不在目录中）</summary>
    public IReadOnlyList<SelectListItem> Parsers { get; init; } = [];

    /// <summary>编辑时任务当前的目标数据类型</summary>
    public CollectTargetType? TargetType { get; init; }

    /// <summary>按表单中的 cron 算出的接下来几次执行时间（采集设计 §3.2）</summary>
    public CronPreview Preview { get; init; } = new(null, []);
}
