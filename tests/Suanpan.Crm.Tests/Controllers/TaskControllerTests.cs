using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// FR-002 主流程第 5 步、采集设计 §8.2："立即执行"以当前登录名作为触发人，跳到运行详情，记入操作日志；
/// Trigger 只能触发日历排期、公布值类任务（由服务判断），TriggerAny 不限。运行记录服务用替身，创建和下发在 Suanpan.Services.Tests 中测试
/// </summary>
public class TaskControllerTests
{
    [Theory]
    [InlineData("Trigger", "collect:task:trigger", false)]
    [InlineData("TriggerAny", "collect:task:trigger_any", true)]
    public async Task Trigger_QueuesRunAsCurrentUserAndOpensDetail(string action, string permCode, bool anyTargetType)
    {
        await using var app = new CrmApp();
        app.Auth.AddAccount(15, SignInStep.VerifyTotp);
        app.Permissions.Grant(15, permCode);
        var browser = await SignedInBrowserAsync(app, "user15");

        var response = await SubmitAsync(browser, "/", new(), action: $"/Collect/Task/{action}/5");

        Assert.Equal("/Collect/Run/Detail/42", response.RedirectPath());
        Assert.Equal((5L, "user15", anyTargetType), Assert.Single(app.CollectRuns.Triggered));
        var log = Assert.Single(app.OpLogs.Written, l => l.Action == "立即执行采集任务");
        Assert.Equal((permCode, true), (log.PermCode, log.IsSuccess));
    }
}
