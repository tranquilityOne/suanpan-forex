using Microsoft.AspNetCore.Razor.TagHelpers;
using Suanpan.Crm.Mvc;
using Suanpan.Crm.TagHelpers;
using Xunit;

namespace Suanpan.Crm.Tests.TagHelpers;

public class UtcTimeTagHelperTests
{
    /// <summary>库里读出的时间 Kind 为 Unspecified，按 UTC 换算成操作者时区（Asia/Shanghai，UTC+8）</summary>
    [Fact]
    public void ShowsUtcValueInOperatorTimeZone()
    {
        var output = Render(new DateTime(2026, 10, 17, 17, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal("time", output.TagName);
        Assert.Equal("2026-10-18 01:00", output.Content.GetContent());
        Assert.Equal("2026-10-17T17:00:00Z", output.Attributes["datetime"].Value);
    }

    [Fact]
    public void EmptyValueShowsDash()
    {
        var output = Render(null);

        Assert.Equal("-", output.Content.GetContent());
    }

    private static TagHelperOutput Render(DateTime? value)
    {
        var helper = new UtcTimeTagHelper(new DefaultOperatorTimeZone()) { Value = value };
        var context = new TagHelperContext([], new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput("utc-time", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.Process(context, output);
        return output;
    }
}
