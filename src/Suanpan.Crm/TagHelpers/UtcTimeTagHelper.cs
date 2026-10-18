using System.Globalization;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Suanpan.Crm.Mvc;

namespace Suanpan.Crm.TagHelpers;

/// <summary>
/// 把库里的 UTC 时间按操作者时区显示，悬停显示 UTC 原值（前端约定 §7.2）：
/// <code>
/// &lt;utc-time value="@row.CreatedAt" /&gt;                         2026-10-18 09:00
/// &lt;utc-time value="@row.CreatedAt" format="yyyy-MM-dd HH:mm:ss" /&gt;
/// </code>
/// 值为空时显示"-"。从库中读出、Kind 为 Unspecified 的时间按 UTC 处理。
/// </summary>
[HtmlTargetElement("utc-time", TagStructure = TagStructure.WithoutEndTag)]
public sealed class UtcTimeTagHelper(IOperatorTimeZone operatorTimeZone) : TagHelper
{
    public DateTime? Value { get; set; }

    public string Format { get; set; } = "yyyy-MM-dd HH:mm";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagMode = TagMode.StartTagAndEndTag;

        if (Value is not { } value)
        {
            output.TagName = "span";
            output.Attributes.SetAttribute("class", "text-secondary");
            output.Content.SetContent("-");
            return;
        }

        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, operatorTimeZone.TimeZone);

        output.TagName = "time";
        output.Attributes.SetAttribute("datetime", utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        output.Attributes.SetAttribute("title", "UTC " + utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        output.Content.SetContent(local.ToString(Format, CultureInfo.InvariantCulture));
    }
}
