namespace Suanpan.Services.Reference.Currencies.Models;

/// <summary>新增或修改货币；修改时 Code 只用来定位，不会被改（BR-026）</summary>
public sealed record CurrencyInput(string Code, string NameZh, string NameEn);
