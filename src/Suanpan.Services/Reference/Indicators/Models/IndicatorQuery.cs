using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Reference.Indicators.Models;

/// <summary>指标列表的筛选条件（基础数据设计 §5.2）：关键字匹配编码或中英文名称、简称，不区分大小写</summary>
public sealed record IndicatorQuery(
    string? EconomyCode = null,
    IndicatorCategory? Category = null,
    Importance? Importance = null,
    bool? IsEnabled = null,
    string? Keyword = null,
    int Page = 1,
    int PageSize = 20);
