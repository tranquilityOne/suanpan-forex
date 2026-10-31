using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Shared.Mvc;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 预期值录入（日历设计 §9.4）：清单中行内保存只返回这一行（HTMX，BR-059），事件详情中的表单整页提交后回到详情；
/// 更正带原因，记入操作日志（原因列）。预期值服务用替身；数据库中的结果在 Suanpan.Services.Tests 的 ForecastServiceTests 中测试
/// </summary>
public class ForecastControllerTests
{
    private const long EventId = 9;

    /// <summary>没有保存权限时清单只读，有保存权限时每行一个表单</summary>
    [Fact]
    public async Task Index_ShowsFormsOnlyWithSavePermission()
    {
        await using var app = new CrmApp();
        var reader = await SignedInAsync(app, 24, "calendar:forecast:index");
        var editor = await SignedInAsync(app, 25, "calendar:forecast:index", "calendar:forecast:save");

        var readOnly = await reader.GetStringAsync("/Calendar/Forecast", Ct);
        var editable = await editor.GetStringAsync("/Calendar/Forecast", Ct);

        Assert.Equal((false, true), (readOnly.Contains("hx-post"), editable.Contains($"hx-post=\"/Calendar/Forecast/Save/{EventId}\"")));
    }

    /// <summary>HTMX 保存成功：只返回这一行，带"定位下一条"的标记；参数和操作日志与整页提交相同</summary>
    [Fact]
    public async Task SaveHtmx_ReturnsSavedRow()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, 24, "calendar:forecast:index", "calendar:forecast:save");
        browser.DefaultRequestHeaders.Add("HX-Request", "true");

        var response = await SubmitAsync(browser, "/Calendar/Forecast",
            new() { ["Forecast"] = "0.3", ["Note"] = "路透调查", ["RowVersion"] = "6" }, action: $"/Calendar/Forecast/Save/{EventId}");

        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.Equal((true, false), (html.Contains("data-focus=\"next\""), html.Contains("<html")));
        var (id, input, rowVersion) = Assert.Single(app.Forecasts.Saved);
        Assert.Equal((EventId, (decimal?)0.3m, "路透调查", 6), (id, input.Forecast, input.Note, rowVersion));
        Assert.Single(app.OpLogs.Written, l => l.Action == "录入预期值" && l.PermCode == "calendar:forecast:save");
    }

    /// <summary>HTMX 保存出错：这一行保留填写的值和提交时的 row_version，标出错误并聚焦自己（ERR-FC-001、ERR-FC-003）</summary>
    [Fact]
    public async Task SaveHtmx_Error_KeepsInputAndRowVersion()
    {
        await using var app = new CrmApp();
        app.Forecasts.Result = ManageResult.Fail("格式不对", "Forecast");
        var browser = await SignedInAsync(app, 24, "calendar:forecast:index", "calendar:forecast:save");
        browser.DefaultRequestHeaders.Add("HX-Request", "true");

        var response = await SubmitAsync(browser, "/Calendar/Forecast",
            new() { ["Forecast"] = "0.15", ["RowVersion"] = "5" }, action: $"/Calendar/Forecast/Save/{EventId}");

        var html = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("data-focus=\"self\"", html);
        Assert.Contains("is-invalid", html);
        Assert.Contains("value=\"0.15\"", html);
        Assert.Contains("name=\"RowVersion\" value=\"5\"", html);
    }

    /// <summary>事件详情中的表单：整页提交，保存后回到详情</summary>
    [Fact]
    public async Task SaveFromDetail_RedirectsToDetail()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, 24, "calendar:event:detail", "calendar:forecast:save");

        var response = await SubmitAsync(browser, $"/Calendar/Event/Detail/{EventId}",
            new() { ["Forecast"] = "0.3", ["RowVersion"] = "6" }, action: $"/Calendar/Forecast/Save/{EventId}");

        Assert.Equal($"/Calendar/Event/Detail/{EventId}", response.RedirectPath());
        Assert.Single(app.Forecasts.Saved);
    }

    /// <summary>公布后更正（BR-057）：带原因和读取时的 row_version，原因记入操作日志</summary>
    [Fact]
    public async Task Correct_PassesReasonAndRowVersion()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, 24, "calendar:forecast:correct");

        var response = await SubmitAsync(browser, $"/Calendar/Forecast/Correct/{EventId}",
            new() { ["Forecast"] = "0.2", ["Reason"] = "录入时填错", ["RowVersion"] = "6" });

        Assert.Equal($"/Calendar/Event/Detail/{EventId}", response.RedirectPath());
        var (id, input, reason, rowVersion) = Assert.Single(app.Forecasts.Corrected);
        Assert.Equal((EventId, (decimal?)0.2m, "录入时填错", 6), (id, input.Forecast, reason, rowVersion));
        var log = Assert.Single(app.OpLogs.Written, l => l.Action == "更正预期值");
        Assert.Equal(("录入时填错", "calendar:forecast:correct"), (log.Reason, log.PermCode));
    }

    private static async Task<HttpClient> SignedInAsync(CrmApp app, long userId, params string[] permCodes)
    {
        app.Auth.AddAccount(userId, SignInStep.VerifyTotp);
        app.Permissions.Grant(userId, permCodes);
        var calendarEvent = new CalendarEvent
        {
            Id = EventId, IndicatorCode = "US.CPI.MOM", ReferencePeriod = "2026-10", Status = CalendarEventStatus.Scheduled, TimeZone = "America/New_York",
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
        app.Forecasts.Rows[EventId] = new EventRow(
            calendarEvent, indicator.ShortName, indicator.EconomyCode, indicator.Importance, indicator.ValueType, indicator.Unit, indicator.Decimals);
        return await SignedInBrowserAsync(app, $"user{userId}");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
