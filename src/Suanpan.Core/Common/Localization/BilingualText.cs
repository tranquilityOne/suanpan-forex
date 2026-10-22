namespace Suanpan.Core.Common.Localization;

/// <summary>
/// 中英文成对的文本（后端约定 §6.3，基础数据设计 §3.7）：数据库中每种语言一列（如 name_zh、name_en），
/// 实体中合成这一个属性。按语言取文字时用 <see cref="Get"/>，不直接读某一种语言的属性，
/// 以后增加语言时只需改这里和存储。
/// </summary>
public sealed record BilingualText(string Zh, string En)
{
    public string Get(ContentLanguage language) => language switch
    {
        ContentLanguage.Zh => Zh,
        ContentLanguage.En => En,
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "不支持的语言"),
    };
}
