using Suanpan.Core.Common.Localization;

namespace Suanpan.Services.Reference.Economies.Models;

/// <summary>经济体列表的一行；IndicatorCount 为属于该经济体的指标数，大于 0 时不能删除</summary>
public sealed record EconomyRow(
    string Code,
    BilingualText Name,
    string CurrencyCode,
    string TimeZone,
    short Sort,
    bool IsEnabled,
    int IndicatorCount);
