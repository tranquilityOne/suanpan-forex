using Suanpan.Core.Reference.Entities;
using Suanpan.Services.Reference.IndicatorSources.Models;

namespace Suanpan.Crm.Models.Reference;

public sealed class IndicatorDetailViewModel
{
    public required Indicator Indicator { get; init; }

    /// <summary>所属经济体；查不到时只显示编码</summary>
    public Economy? Economy { get; init; }

    /// <summary>数据源映射，按优先级排序（BR-034）</summary>
    public IReadOnlyList<IndicatorSourceRow> Sources { get; init; } = [];
}
