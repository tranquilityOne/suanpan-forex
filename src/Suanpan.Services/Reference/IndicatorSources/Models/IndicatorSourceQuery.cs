namespace Suanpan.Services.Reference.IndicatorSources.Models;

/// <summary>映射列表的筛选条件（基础数据设计 §6.2）</summary>
public sealed record IndicatorSourceQuery(
    string? IndicatorCode = null,
    long? SourceId = null,
    int Page = 1,
    int PageSize = 20);
