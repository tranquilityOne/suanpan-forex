namespace Suanpan.Core.Common.Validation;

/// <summary>
/// 领域规则的一条校验失败。Field 为出错的表单字段名，与实体的属性名一致；中英文字段为属性名加 Zh / En（如 NameZh）。
/// 服务转成 ManageResult.Fail(Message, Field)，页面据此把提示显示在对应输入框下（后端约定 §7）
/// </summary>
public sealed record FieldError(string Field, string Message);
