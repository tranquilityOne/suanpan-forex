using System.Net;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;

namespace Suanpan.Crm.Tests;

public class ProgramTests
{
    /// <summary>操作日志过滤器注册为全局过滤器，并能读到自动生成的权限码（FR-029 #1）</summary>
    [Fact]
    public async Task OpLogAction_IsLoggedWithDerivedPermCode()
    {
        await using var app = new CrmApp();
        app.Permissions.Grant(7, "sys:pipeline_probe:save");

        var response = await app.CreateBrowser().SendAsync(
            Requests.Post("/Sys/PipelineProbe/Save", app.CreateCookie(7), new Dictionary<string, string> { ["name"] = "x" }), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = Assert.Single(app.OpLogs.Written);
        Assert.Equal(("测试", "探针保存", "sys:pipeline_probe:save", 7L), (log.Module, log.Action, log.PermCode, log.UserId));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
