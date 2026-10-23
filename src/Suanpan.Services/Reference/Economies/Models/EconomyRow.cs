using Suanpan.Core.Common.Localization;

namespace Suanpan.Services.Reference.Economies.Models;

/// <summary>经济体列表的一行；IndicatorCount、DataSourceCount 为属于该经济体的指标数、数据源数，任一大于 0 时不能删除</summary>
public sealed record EconomyRow(
    string Code,
    BilingualText Name,
    string CurrencyCode,
    string TimeZone,
    short Sort,
    bool IsEnabled,
    int IndicatorCount,
    int DataSourceCount)
{
    public bool IsReferenced => IndicatorCount > 0 || DataSourceCount > 0;
}
