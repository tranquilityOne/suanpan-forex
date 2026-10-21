using Microsoft.AspNetCore.Razor.TagHelpers;
using Suanpan.Crm.Auth;

namespace Suanpan.Crm.TagHelpers;

/// <summary>
/// 按权限隐藏按钮（前端约定 §6，BR-152）：当前用户没有指定权限码时不输出该元素，管理员总是输出。
/// <code>
/// &lt;a asp-action="Edit" asp-permission="sys:user:edit"&gt;编辑&lt;/a&gt;
/// &lt;confirm-form asp-action="Delete" asp-permission="sys:role:delete" ... /&gt;
/// </code>
/// 只影响显示，后端校验照常进行（BR-145）。
/// </summary>
[HtmlTargetElement("*", Attributes = AttributeName)]
public sealed class PermissionTagHelper(CurrentPermissions permissions) : TagHelper
{
    private const string AttributeName = "asp-permission";

    /// <summary>
    /// 在同一元素的其他标签助手之后执行：confirm-form 这类助手会重新生成标签，先执行的话隐藏会被覆盖
    /// </summary>
    public override int Order => int.MaxValue;

    [HtmlAttributeName(AttributeName)]
    public string PermCode { get; set; } = string.Empty;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (!await permissions.HasAsync(PermCode))
        {
            output.SuppressOutput();
        }
    }
}
