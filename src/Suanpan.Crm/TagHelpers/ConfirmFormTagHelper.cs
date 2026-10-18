using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Suanpan.Crm.TagHelpers;

/// <summary>
/// 危险操作（删除、撤回、停用、切换开关）的按钮（前端约定 §5.3、§6）：POST 表单 + 防伪令牌 + 二次确认弹窗。
/// <code>
/// &lt;confirm-form asp-action="Disable" asp-route-id="@user.Id" message="停用后该账号立即退出登录" button-text="停用"&gt;
///     &lt;input type="hidden" name="reason" value="离职" /&gt;    （可选：随表单提交的其他字段）
/// &lt;/confirm-form&gt;
/// </code>
/// 提示文案写清后果。目标地址按 asp-area / asp-controller / asp-action / asp-route-* 生成，生成不出来时直接报错，
/// 避免表单退回到提交当前页面。
/// </summary>
[HtmlTargetElement("confirm-form")]
public sealed class ConfirmFormTagHelper(IHtmlGenerator htmlGenerator, IUrlHelperFactory urlHelperFactory) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    [HtmlAttributeName("asp-area")]
    public string? Area { get; set; }

    [HtmlAttributeName("asp-controller")]
    public string? Controller { get; set; }

    [HtmlAttributeName("asp-action")]
    public string? Action { get; set; }

    [HtmlAttributeName("asp-all-route-data", DictionaryAttributePrefix = "asp-route-")]
    public IDictionary<string, string?> RouteValues { get; set; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>弹窗中的提示，写清操作的后果</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>按钮文字，也用作弹窗中确认按钮的文字</summary>
    public string ButtonText { get; set; } = "确定";

    public string ButtonClass { get; set; } = "btn btn-sm btn-outline-danger";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new InvalidOperationException("<confirm-form> 必须写 message，说明操作的后果");
        }

        var routeValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in RouteValues)
        {
            routeValues[key] = value;
        }

        if (Area is not null)
        {
            routeValues["area"] = Area;
        }

        var url = urlHelperFactory.GetUrlHelper(ViewContext).Action(Action, Controller, routeValues)
                  ?? throw new InvalidOperationException($"<confirm-form> 生成不出地址：{Area}/{Controller}/{Action}");

        var childContent = await output.GetChildContentAsync();

        output.TagName = "form";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("method", "post");
        output.Attributes.SetAttribute("action", url);
        output.Attributes.SetAttribute("class", "d-inline");
        output.Attributes.SetAttribute("data-confirm", Message);
        output.Attributes.SetAttribute("data-confirm-button", ButtonText);

        var button = new TagBuilder("button");
        button.Attributes["type"] = "submit";
        button.AddCssClass(ButtonClass);
        button.InnerHtml.Append(ButtonText);

        output.Content.AppendHtml(htmlGenerator.GenerateAntiforgery(ViewContext));
        output.Content.AppendHtml(childContent);
        output.Content.AppendHtml(button);
    }
}
