using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Suanpan.Crm.TagHelpers;

/// <summary>
/// 状态徽章（前端约定 §6、§7.1），颜色统一：
/// <code>
/// &lt;status-badge value="@user.Status" /&gt;                    按 StatusBadges 中登记的文字和颜色
/// &lt;status-badge tone="Failure" text="失败" /&gt;              不对应枚举时直接指定
/// </code>
/// </summary>
[HtmlTargetElement("status-badge", TagStructure = TagStructure.WithoutEndTag)]
public sealed class StatusBadgeTagHelper : TagHelper
{
    /// <summary>状态枚举值</summary>
    public Enum? Value { get; set; }

    /// <summary>不用枚举时直接指定颜色</summary>
    public BadgeTone Tone { get; set; } = BadgeTone.Neutral;

    /// <summary>显示文字；同时给了 value 时以这里为准</summary>
    public string? Text { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (tone, text) = Value is null ? (Tone, string.Empty) : StatusBadges.Describe(Value);

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "badge " + StatusBadges.CssClass(tone));
        output.Content.SetContent(Text ?? text);
    }
}
