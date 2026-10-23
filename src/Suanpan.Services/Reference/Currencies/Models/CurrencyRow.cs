using Suanpan.Core.Common.Localization;

namespace Suanpan.Services.Reference.Currencies.Models;

/// <summary>货币列表的一行；EconomyCount 为使用该货币的经济体数，大于 0 时不能删除</summary>
public sealed record CurrencyRow(string Code, BilingualText Name, bool IsEnabled, int EconomyCount);
