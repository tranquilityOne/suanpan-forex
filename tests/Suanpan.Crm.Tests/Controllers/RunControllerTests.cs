using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Collect.Runs.Models;
using Xunit;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 运行详情的自动刷新（前端约定 §4.1"手动触发采集"，采集设计 §4.4）：HTMX 请求只返回详情区块；
/// 排队、运行中的区块带定时刷新，结束后的区块不带，替换上去就停止刷新
/// </summary>
public class RunControllerTests
{
    private const long UserId = 16;

    [Theory]
    [InlineData(CollectRunStatus.Running, true)]
    [InlineData(CollectRunStatus.Succeeded, false)]
    public async Task Detail_HtmxReturnsBlockThatRefreshesUntilFinished(CollectRunStatus status, bool refreshing)
    {
        await using var app = new CrmApp();
        app.Permissions.Grant(UserId, "collect:run:detail");
        app.CollectRuns.Runs[7] = new CollectRunDetail(
            new CollectRun { Id = 7, TaskId = 3, SourceId = 2, Trigger = CollectRunTrigger.Manual, Status = status },
            "BLS 发布日程", CollectTargetType.Calendar, "BLS 官网", []);

        var response = await app.CreateBrowser().SendAsync(Requests.Get("/Collect/Run/Detail/7", app.CreateCookie(UserId)).AsHtmx(), Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.True(response.IsSuccessStatusCode);
        Assert.DoesNotContain("<html", html);
        Assert.Contains("id=\"run-detail\"", html);
        Assert.Equal(refreshing, html.Contains("hx-trigger=", StringComparison.Ordinal));
    }

    /// <summary>
    /// 一路盯盘的请求记录（采集设计 §8.1）：默认只列首末各 5 条和内容有变化的（第 7 条），第 6 条折叠；展开全部后列出，
    /// 自动刷新的地址也带着"全部"。耗时按序号设为 900N 毫秒，用来认出是哪一条
    /// </summary>
    [Fact]
    public async Task Detail_CollapsesUnchangedWatchAttempts()
    {
        await using var app = new CrmApp();
        app.Permissions.Grant(UserId, "collect:run:detail");
        var run = new CollectRun
        {
            Id = 8, TaskId = 3, SourceId = 2, Trigger = CollectRunTrigger.Watch, ScheduledFor = new DateTime(2026, 11, 6, 13, 30, 0, DateTimeKind.Utc),
            Status = CollectRunStatus.Running, AttemptCount = 12,
            Attempts = Enumerable.Range(1, 12)
                .Select(no => new CollectAttempt { No = (short)no, DurationMs = 9000 + no, HttpStatus = 200, Changed = no is 1 or 7 })
                .ToList(),
        };
        app.CollectRuns.Runs[8] = new CollectRunDetail(run, "BLS 公布值", CollectTargetType.Release, "BLS API", []);
        var browser = app.CreateBrowser();

        var collapsed = await browser.SendAsync(Requests.Get("/Collect/Run/Detail/8", app.CreateCookie(UserId)).AsHtmx(), Ct);
        var all = await browser.SendAsync(Requests.Get("/Collect/Run/Detail/8?all=true", app.CreateCookie(UserId)).AsHtmx(), Ct);
        var collapsedHtml = await collapsed.Content.ReadAsStringAsync(Ct);
        var allHtml = await all.Content.ReadAsStringAsync(Ct);

        Assert.All(new[] { "9005", "9007", "9008" }, ms => Assert.Contains(ms, collapsedHtml));
        Assert.DoesNotContain("9006", collapsedHtml);
        Assert.Contains("all=True", collapsedHtml);
        Assert.Contains("9006", allHtml);
        Assert.Contains("hx-get=\"/Collect/Run/Detail/8?all=True\"", allHtml);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
