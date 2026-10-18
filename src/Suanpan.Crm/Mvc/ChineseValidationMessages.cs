using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Suanpan.Crm.Mvc;

/// <summary>
/// 表单校验的中文默认提示（前端约定 §5.2）：ViewModel 只标 [Display] 中文名，常用校验特性没写 ErrorMessage 时
/// 用这里的默认提示，服务端和客户端（data-val-* 属性）显示的是同一句话。特殊场景在特性上单独写 ErrorMessage。
/// </summary>
public static class ChineseValidationMessages
{
    public static void UseChineseValidationMessages(this MvcOptions options)
    {
        options.ModelMetadataDetailsProviders.Add(new DefaultMessageProvider());

        var messages = options.ModelBindingMessageProvider;
        messages.SetValueMustNotBeNullAccessor(field => $"请填写{field}");
        messages.SetMissingBindRequiredValueAccessor(field => $"请填写{field}");
        messages.SetMissingKeyOrValueAccessor(() => "缺少必填的值");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "缺少请求内容");
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"{field}的值“{value}”无效");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"值“{value}”无效");
        messages.SetUnknownValueIsInvalidAccessor(field => $"{field}的值无效");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "值无效");
        messages.SetValueIsInvalidAccessor(value => $"值“{value}”无效");
        messages.SetValueMustBeANumberAccessor(field => $"{field}必须是数字");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "必须是数字");
    }

    /// <summary>
    /// 给没有自定义提示的校验特性填上中文提示。排在 DataAnnotations 提供器之后，所以隐式添加的 [Required] 也会处理到。
    /// {0} 为 [Display] 的名称。
    /// </summary>
    private sealed class DefaultMessageProvider : IValidationMetadataProvider
    {
        private const string RequiredMessage = "请填写{0}";

        /// <summary>各特性类型新建实例时自带的提示（邮箱、电话、网址等特性读取 ErrorMessage 时返回的是内置的英文提示）</summary>
        private static readonly ConcurrentDictionary<Type, string?> BuiltInMessages = new();

        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            var validators = context.ValidationMetadata.ValidatorMetadata;

            // 非可空值类型（int、bool、DateTime 等）的必填：MVC 的客户端校验会另外新建一个不带提示的 [Required]，
            // 这里显式加上带中文提示的，客户端就改用这一个
            if (context.Key.MetadataKind == ModelMetadataKind.Property
                && context.Key.ModelType.IsValueType
                && Nullable.GetUnderlyingType(context.Key.ModelType) is null
                && !validators.OfType<RequiredAttribute>().Any())
            {
                validators.Add(new RequiredAttribute { ErrorMessage = RequiredMessage });
            }

            foreach (var attribute in validators.OfType<ValidationAttribute>())
            {
                if (attribute.ErrorMessageResourceName is not null || !UsesBuiltInMessage(attribute))
                {
                    continue;
                }

                var message = attribute switch
                {
                    RequiredAttribute => RequiredMessage,
                    StringLengthAttribute { MinimumLength: > 0 } => "{0}的长度必须在 {2} 到 {1} 个字符之间",
                    StringLengthAttribute or MaxLengthAttribute => "{0}最多 {1} 个字符",
                    MinLengthAttribute => "{0}至少 {1} 个字符",
                    RangeAttribute => "{0}必须在 {1} 到 {2} 之间",
                    CompareAttribute => "{0}与{1}不一致",
                    EmailAddressAttribute or PhoneAttribute or UrlAttribute or RegularExpressionAttribute => "{0}的格式不正确",
                    _ => null,
                };
                if (message is not null)
                {
                    attribute.ErrorMessage = message;
                }
            }
        }

        private static bool UsesBuiltInMessage(ValidationAttribute attribute)
            => attribute.ErrorMessage is null
               || attribute.ErrorMessage == BuiltInMessages.GetOrAdd(attribute.GetType(), type =>
                   type.GetConstructor(Type.EmptyTypes) is null ? null : ((ValidationAttribute)Activator.CreateInstance(type)!).ErrorMessage);
    }
}
