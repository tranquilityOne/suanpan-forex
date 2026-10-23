using Suanpan.Core.Common.Localization;

namespace Suanpan.Services.Reference.Currencies.Models;

/// <summary>货币列表的一行；EconomyCount、IndicatorCount 为使用该货币的经济体数、指标数，任一大于 0 时不能删除</summary>
public sealed record CurrencyRow(string Code, BilingualText Name, bool IsEnabled, int EconomyCount, int IndicatorCount)
{
    public bool IsReferenced => EconomyCount > 0 || IndicatorCount > 0;
}
