namespace Suanpan.Core.Reference.Rules;

/// <summary>
/// 指标保存时的一条校验失败。Field 为出错的表单字段名，与 Indicator 的属性名一致；
/// 中英文字段为属性名加 Zh / En（如 NameZh），页面据此把提示显示在对应输入框下
/// </summary>
public sealed record IndicatorRuleError(string Field, string Message);
