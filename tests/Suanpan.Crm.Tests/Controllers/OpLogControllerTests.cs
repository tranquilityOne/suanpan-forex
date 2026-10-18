using System.Net;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Sys.Logging.Models;
using Xunit;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 完成标准（JAN-70）：日志按筛选条件导出，导出动作本身记入操作日志（BR-150）。
/// 日志服务用替身，查询和 CSV 内容在 Suanpan.Services.Tests 中测试。
/// </summary>
public class OpLogControllerTests
{
    [Fact]
    public async Task Export_StreamsCsvForTheFilter_AndIsOpLogged()
    {
        await using var app = new CrmApp();
        app.Permissions.Grant(1, "sys:op_log:export");
        var browser = app.CreateBrowser();

        var response = await browser.SendAsync(Requests.Get(
            "/Sys/OpLog/Export?Username=alice&Module=系统管理&ActionName=修改配置&IsSuccess=false&From=2026-10-17&To=2026-10-18",
            app.CreateCookie(1)), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(RecordingOpLogService.ExportContent, await response.Content.ReadAsStringAsync(Ct));

        // 日期按操作者时区（北京时间）换算成 UTC 区间
        var query = Assert.Single(app.OpLogs.ExportQueries);
        Assert.Equal(new OpLogQuery("alice", "系统管理", "修改配置", false,
            new DateTime(2026, 10, 16, 16, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 18, 16, 0, 0, DateTimeKind.Utc)), query);

        var log = Assert.Single(app.OpLogs.Written);
        Assert.Equal(("导出操作日志", "sys:op_log:export", true), (log.Action, log.PermCode, log.IsSuccess));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
