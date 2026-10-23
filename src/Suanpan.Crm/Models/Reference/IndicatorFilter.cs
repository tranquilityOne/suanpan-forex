using Suanpan.Core.Reference.Enums;
using Suanpan.Services.Reference.Indicators.Models;

namespace Suanpan.Crm.Models.Reference;

/// <summary>指标列表的筛选条件（基础数据设计 §6）</summary>
public sealed class IndicatorFilter
{
    public string? EconomyCode { get; set; }

    public IndicatorCategory? Category { get; set; }

    public Importance? Importance { get; set; }

    public bool? IsEnabled { get; set; }

    /// <summary>编码或中英文名称、简称包含的文字</summary>
    public string? Keyword { get; set; }

    public IndicatorQuery ToQuery(int page = 1) => new(EconomyCode, Category, Importance, IsEnabled, Keyword, page);
}
