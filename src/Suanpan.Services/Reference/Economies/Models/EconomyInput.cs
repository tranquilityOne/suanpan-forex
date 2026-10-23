namespace Suanpan.Services.Reference.Economies.Models;

/// <summary>新增或修改经济体；修改时 Code 只用来定位，不会被改（BR-026）</summary>
public sealed record EconomyInput(string Code, string NameZh, string NameEn, string CurrencyCode, string TimeZone, short Sort);
