using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Suanpan.Crm.Mvc;
using Xunit;

namespace Suanpan.Crm.Tests.Mvc;

/// <summary>校验特性没有自定义提示时用中文默认提示；服务端和客户端用的是同一个特性实例的提示</summary>
public class ChineseValidationMessagesTests
{
    private static readonly IModelMetadataProvider MetadataProvider = new ServiceCollection()
        .AddLogging()
        .AddControllersWithViews(options => options.UseChineseValidationMessages())
        .Services.BuildServiceProvider()
        .GetRequiredService<IModelMetadataProvider>();

    [Theory]
    [InlineData(nameof(SampleForm.Name), typeof(RequiredAttribute), "请填写名称")]
    [InlineData(nameof(SampleForm.Name), typeof(StringLengthAttribute), "名称的长度必须在 2 到 20 个字符之间")]
    [InlineData(nameof(SampleForm.Quantity), typeof(RequiredAttribute), "请填写数量")]   // int 的隐式必填
    [InlineData(nameof(SampleForm.Quantity), typeof(RangeAttribute), "数量必须在 1 到 100 之间")]
    [InlineData(nameof(SampleForm.Email), typeof(EmailAddressAttribute), "邮箱的格式不正确")]   // 自带英文提示的特性
    [InlineData(nameof(SampleForm.Code), typeof(RegularExpressionAttribute), "编码只能是大写字母")]   // 已自定义的不覆盖
    public void DefaultMessagesAreChinese(string property, Type attributeType, string expected)
    {
        var metadata = MetadataProvider.GetMetadataForProperty(typeof(SampleForm), property);

        var attribute = Assert.Single(metadata.ValidatorMetadata.OfType<ValidationAttribute>(), a => a.GetType() == attributeType);

        Assert.Equal(expected, attribute.FormatErrorMessage(metadata.GetDisplayName()));
    }

    public sealed class SampleForm
    {
        [Required]
        [StringLength(20, MinimumLength = 2)]
        [Display(Name = "名称")]
        public string? Name { get; set; }

        [Range(1, 100)]
        [Display(Name = "数量")]
        public int Quantity { get; set; }

        [EmailAddress]
        [Display(Name = "邮箱")]
        public string? Email { get; set; }

        [RegularExpression("^[A-Z]+$", ErrorMessage = "{0}只能是大写字母")]
        [Display(Name = "编码")]
        public string? Code { get; set; }
    }
}
