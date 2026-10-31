namespace Suanpan.Services.Calendar.Forecasts.Models;

/// <summary>待录入预期值的清单（BR-055）：默认只列高重要性的，AllImportance 为 true 时列出全部重要性（中低重要性选择性录入）</summary>
public sealed record ForecastQuery(bool AllImportance = false, int Page = 1, int PageSize = 50);

/// <summary>录入的预期值（区间型指标连同上限）和来源说明（选填，≤ 100 字，仅内部可见，BR-054）</summary>
public sealed record ForecastInput(decimal? Forecast, decimal? ForecastUpper, string? Note);
