using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Calendar.Events.Models;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 取消待确认事件（日历设计 §9.1）：原因必填（ERR-EVT-008），带读取时的 row_version（ERR-EVT-009），原因记入操作日志的原因列。
/// 日历事件服务用替身；状态流转和 row_version 的校验在 Suanpan.Services.Tests 中测试
/// </summary>
public class UnconfirmedControllerTests
{
    private const long EventId = 5;

    [Fact]
    public async Task Cancel_WithoutReasonDoesNotCallService()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app);

        var response = await SubmitAsync(browser, $"/Calendar/Unconfirmed/Cancel/{EventId}", new() { ["Reason"] = " ", ["RowVersion"] = "3" });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(app.CalendarEvents.Cancelled);
    }

    [Fact]
    public async Task Cancel_PassesRowVersionAndLogsReason()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app);

        var response = await SubmitAsync(browser, $"/Calendar/Unconfirmed/Cancel/{EventId}", new() { ["Reason"] = "官方宣布停发", ["RowVersion"] = "3" });

        Assert.Equal("/Calendar/Unconfirmed", response.RedirectPath());
        Assert.Equal((EventId, "官方宣布停发", 3), Assert.Single(app.CalendarEvents.Cancelled));
        var log = Assert.Single(app.OpLogs.Written, l => l.Action == "取消待确认事件");
        Assert.Equal(("官方宣布停发", "calendar:unconfirmed:cancel", true), (log.Reason, log.PermCode, log.IsSuccess));
    }

    private static async Task<HttpClient> SignedInAsync(CrmApp app)
    {
        app.Auth.AddAccount(21, SignInStep.VerifyTotp);
        app.Permissions.Grant(21, "calendar:unconfirmed:cancel");
        app.CalendarEvents.Unconfirmed[EventId] = new UnconfirmedEventRow(
            EventId, "US.NFP", new BilingualText("非农", "NFP"), "2026-10", new DateTime(2026, 11, 6, 13, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 6, 8, 30, 0), "America/New_York", TimePrecision.Minute, 1, "BLS", null, 3);
        return await SignedInBrowserAsync(app, "user21");
    }
}
