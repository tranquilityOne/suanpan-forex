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
            "BLS 发布日程", CollectTargetType.Calendar, "BLS 官网");

        var response = await app.CreateBrowser().SendAsync(Requests.Get("/Collect/Run/Detail/7", app.CreateCookie(UserId)).AsHtmx(), Ct);
        var html = await response.Content.ReadAsStringAsync(Ct);

        Assert.True(response.IsSuccessStatusCode);
        Assert.DoesNotContain("<html", html);
        Assert.Contains("id=\"run-detail\"", html);
        Assert.Equal(refreshing, html.Contains("hx-trigger=", StringComparison.Ordinal));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
