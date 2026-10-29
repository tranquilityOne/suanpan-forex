using System.Text;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Collect.Snapshots.Models;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 快照页面（采集设计 §5.5、§8.1）：内容只以纯文本显示、下载按附件（FR-004 验收第 4 条，BR-025）；重新解析带上范围和解析器，
/// 记入操作日志后跳到运行详情。快照服务、运行记录服务用替身；筛选、发起时的校验在 Suanpan.Services.Tests 中测试
/// </summary>
public class SnapshotControllerTests
{
    private const string Script = "<script>alert('x')</script>";

    [Fact]
    public async Task Detail_ShowsScriptAsEscapedText()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, "collect:snapshot:detail");

        var html = await browser.GetStringAsync("/Collect/Snapshot/Detail/5", TestContext.Current.CancellationToken);

        Assert.DoesNotContain(Script, html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task Download_IsAttachmentThatBrowsersDoNotSniff()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, "collect:snapshot:download");

        var response = await browser.GetAsync("/Collect/Snapshot/Download/5", TestContext.Current.CancellationToken);

        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(("attachment", "snapshot-5.html"),
            (response.Content.Headers.ContentDisposition?.DispositionType, response.Content.Headers.ContentDisposition?.FileName));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Reparse_PassesSelectionAndParserThenOpensRun()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, "collect:snapshot:detail", "collect:snapshot:reparse");

        var response = await SubmitAsync(browser, "/Collect/Snapshot/Detail/5", new()
        {
            ["Filter.TaskId"] = "3",
            ["Filter.IsValid"] = "true",
            ["Ids[0]"] = "11",
            ["Ids[1]"] = "12",
            ["ParserKey"] = "us.blsweb.schedule",
        }, action: "/Collect/Snapshot/Reparse");

        Assert.Equal("/Collect/Run/Detail/43", response.RedirectPath());
        var (selection, parserKey, triggeredBy) = Assert.Single(app.CollectRuns.Reparsed);
        Assert.Equal(((long?)3, (bool?)true, "us.blsweb.schedule", "user22"), (selection.Filter.TaskId, selection.Filter.IsValid, parserKey, triggeredBy));
        Assert.Equal([11L, 12L], selection.SnapshotIds);
        var log = Assert.Single(app.OpLogs.Written, l => l.Action == "重新解析快照");
        Assert.Equal(("collect:snapshot:reparse", true), (log.PermCode, log.IsSuccess));
    }

    private static async Task<HttpClient> SignedInAsync(CrmApp app, params string[] permCodes)
    {
        app.Auth.AddAccount(22, SignInStep.VerifyTotp);
        app.Permissions.Grant(22, permCodes);
        app.Snapshots.Details[5] = new SnapshotDetail(
            new Snapshot { Id = 5, RunId = 9, TaskId = 3, Url = "https://www.bls.gov/schedule/news_release/empsit.htm", ContentType = "text/html" },
            "BLS 发布日程", "BLS 发布日程", Run: null, Encoding.UTF8.GetBytes($"<html><body>{Script}</body></html>"));
        return await SignedInBrowserAsync(app, "user22");
    }
}
