using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Calendar.Events.Models;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 修改数值、确认采集值（日历设计 §3.7、§9.3）：带上数值、原因和读取时的 row_version，记入操作日志（原因列）。
/// 日历事件服务用替身；数据库中的结果在 Suanpan.Services.Tests 的 CalendarEventEditTests 中测试
/// </summary>
public class ReviewControllerTests
{
    private const long EventId = 9;

    [Fact]
    public async Task Edit_PassesValuesReasonAndRowVersion()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, "calendar:event:edit");

        var response = await SubmitAsync(browser, $"/Calendar/Event/Edit/{EventId}",
            new() { ["Actual"] = "0.4", ["Previous"] = "", ["Reason"] = "官方 API 故障", ["RowVersion"] = "6" });

        Assert.Equal($"/Calendar/Event/Detail/{EventId}", response.RedirectPath());
        var (id, input, reason, rowVersion) = Assert.Single(app.CalendarEvents.Edited);
        Assert.Equal((EventId, (decimal?)0.4m, (decimal?)null, "官方 API 故障", 6), (id, input.Actual, input.Previous, reason, rowVersion));
        var log = Assert.Single(app.OpLogs.Written, l => l.Action == "修改事件数值");
        Assert.Equal(("官方 API 故障", "calendar:event:edit"), (log.Reason, log.PermCode));
    }

    [Fact]
    public async Task Confirm_PassesSourceAndRowVersion()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, "calendar:event:detail", "calendar:review:confirm");

        var response = await SubmitAsync(browser, $"/Calendar/Event/Detail/{EventId}", new() { ["sourceId"] = "2", ["rowVersion"] = "6" },
            action: $"/Calendar/Review/Confirm/{EventId}");

        Assert.Equal("/Calendar/Review", response.RedirectPath());
        Assert.Equal((EventId, 2L, 6), Assert.Single(app.CalendarEvents.Confirmed));
        Assert.Single(app.OpLogs.Written, l => l.Action == "确认采集值" && l.PermCode == "calendar:review:confirm");
    }

    private static async Task<HttpClient> SignedInAsync(CrmApp app, params string[] permCodes)
    {
        app.Auth.AddAccount(24, SignInStep.VerifyTotp);
        app.Permissions.Grant(24, permCodes);
        var calendarEvent = new CalendarEvent
        {
            Id = EventId, IndicatorCode = "US.CPI.MOM", ReferencePeriod = "2026-10", Status = CalendarEventStatus.PendingReview, TimeZone = "America/New_York",
            ScheduledAt = new DateTime(2026, 11, 10, 13, 30, 0, DateTimeKind.Utc), ScheduledLocal = new DateTime(2026, 11, 10, 8, 30, 0),
            TimePrecision = TimePrecision.Minute, RowVersion = 6,
        };
        var indicator = new Indicator
        {
            Code = "US.CPI.MOM", EconomyCode = "US", CurrencyCode = "USD", Name = new BilingualText("CPI 月率", "CPI MoM"),
            ShortName = new BilingualText("CPI 月率", "CPI MoM"), Agency = new BilingualText("劳工统计局", "BLS"),
            ValueType = IndicatorValueType.Value, Unit = IndicatorUnit.Percent, Decimals = 1, Importance = Importance.High,
        };
        app.CalendarEvents.Details[EventId] = new EventDetail(calendarEvent, indicator, [], [], [], new Dictionary<long, string>());
        return await SignedInBrowserAsync(app, "user24");
    }
}
