using System.Net;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Collect.Watch.Models;
using Xunit;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 盯盘状态（采集设计 §8.1，FR-003）：整页有三块，计划中不再列出已经在盯的那一路，不能盯盘的事件标红并写原因；
/// HTMX 请求（每 5 秒刷新）只返回"正在盯盘"区块。盯盘计划服务用替身，数据在 Suanpan.Services.Tests 的 WatchPlanServiceTests 中测试
/// </summary>
public class WatchControllerTests
{
    private const long UserId = 31;

    private static readonly DateTime T = new(2026, 11, 6, 13, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Index_ShowsActivePlanAndRecent()
    {
        await using var app = SetUp();

        var response = await app.CreateBrowser().SendAsync(Requests.Get("/Collect/Watch", app.CreateCookie(UserId)), Ct);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("hx-trigger=\"every 5s [!document.hidden]\"", html);
        Assert.Contains("正在盯的数据源", html);
        Assert.DoesNotContain("已在盯的那一路", html);
        Assert.Contains("稍后盯的数据源", html);
        Assert.Contains("<tr class=\"text-danger\">", html);
        Assert.Contains("测试用的原因", html);
        Assert.Contains("结束了的数据源", html);
        Assert.Contains("20.0 秒", html);
    }

    [Fact]
    public async Task Index_HtmxReturnsActiveBlockOnly()
    {
        await using var app = SetUp();

        var response = await app.CreateBrowser().SendAsync(Requests.Get("/Collect/Watch", app.CreateCookie(UserId)).AsHtmx(), Ct);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

        Assert.True(response.IsSuccessStatusCode);
        Assert.DoesNotContain("<html", html);
        Assert.Contains("id=\"watch-active\"", html);
        Assert.Contains("正在盯的数据源", html);
        Assert.DoesNotContain("稍后盯的数据源", html);
    }

    /// <summary>
    /// 正在盯一路（任务 3、排期 T），计划里同一路和另一路（任务 4），一个不能盯盘的事件；最近结束的一路有一个事件 20 秒后入库
    /// </summary>
    private static CrmApp SetUp()
    {
        var app = new CrmApp();
        app.Permissions.Grant(UserId, "collect:watch:index");
        var timing = new WatchTiming(TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(2));
        var watch = app.Watch;
        watch.Active.Add(Run(1, "正在盯的数据源", taskId: 3, T, CollectRunStatus.Running, latency: null) with
        {
            LastAttempt = new CollectAttempt { No = 1, StartedAt = T.AddSeconds(-60), HttpStatus = 200, Changed = true },
        });
        watch.Groups.Add(new WatchGroup(1, "已在盯的那一路", 3, "公布值", T, timing, WatchRule.Window(T, timing), [Event(11)]));
        watch.Groups.Add(new WatchGroup(2, "稍后盯的数据源", 4, "公布值", T.AddHours(1), timing, WatchRule.Window(T.AddHours(1), timing), [Event(12)]));
        watch.Problems.Add(new WatchProblem(Event(13), T.AddHours(2), WatchProblemKind.NoMapping, "测试用的原因"));
        watch.Recent.Add(Run(2, "结束了的数据源", taskId: 5, T.AddHours(-3), CollectRunStatus.Succeeded, latency: TimeSpan.FromSeconds(20)));
        return app;
    }

    private static WatchRunItem Run(long id, string sourceName, long taskId, DateTime scheduledAt, CollectRunStatus status, TimeSpan? latency)
        => new(id, id, sourceName, taskId, scheduledAt, status, scheduledAt.AddSeconds(-60), status == CollectRunStatus.Running ? null : scheduledAt.AddMinutes(1),
            1, null, null, null,
            [
                new WatchRunEvent(id * 10, "US.NFP", new BilingualText("非农", "NFP"), "2026-10",
                    latency is null ? CalendarEventStatus.Watching : CalendarEventStatus.Released, scheduledAt + latency, latency),
            ]);

    private static WatchEventItem Event(long id)
        => new(id, $"US.T{id}", new BilingualText("测试指标", "Test"), Importance.High, "2026-10", CalendarEventStatus.Scheduled);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
