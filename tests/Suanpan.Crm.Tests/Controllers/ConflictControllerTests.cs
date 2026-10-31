using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Calendar.Conflicts.Models;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 锁定冲突（日历设计 §9.5）：保持锁定、解锁并采用带事件的 row_version，记入操作日志；没有处理权限时只能查看（ERR-AUTH-002）。
/// 冲突服务用替身；数据库中的结果在 Suanpan.Services.Tests 的 LockConflictServiceTests 中测试
/// </summary>
public class ConflictControllerTests
{
    private const long ConflictId = 5;

    /// <summary>采集工程师、只读只有查看权限：列表里看得到两个值，没有处理按钮</summary>
    [Fact]
    public async Task Index_ShowsActionsOnlyWithPermission()
    {
        await using var app = new CrmApp();
        var viewer = await SignedInAsync(app, 24, "calendar:conflict:index");
        var editor = await SignedInAsync(app, 25, "calendar:conflict:index", "calendar:conflict:keep", "calendar:conflict:adopt");

        var readOnly = await viewer.GetStringAsync("/Calendar/Conflict", Ct);
        var editable = await editor.GetStringAsync("/Calendar/Conflict", Ct);

        Assert.Contains("0.4%", readOnly);
        Assert.Contains("0.3%", readOnly);
        Assert.Equal((false, true), (readOnly.Contains("/Calendar/Conflict/Keep/"), editable.Contains($"/Calendar/Conflict/Keep/{ConflictId}")));
        Assert.Contains($"/Calendar/Conflict/Adopt/{ConflictId}", editable);
    }

    [Fact]
    public async Task KeepAndAdopt_PassRowVersionAndLog()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, 24, "calendar:conflict:index", "calendar:conflict:keep", "calendar:conflict:adopt");

        var keep = await SubmitAsync(browser, "/Calendar/Conflict", new() { ["rowVersion"] = "6" }, action: $"/Calendar/Conflict/Keep/{ConflictId}");
        var adopt = await SubmitAsync(browser, "/Calendar/Conflict", new() { ["rowVersion"] = "6" }, action: $"/Calendar/Conflict/Adopt/{ConflictId}");

        Assert.Equal(("/Calendar/Conflict", "/Calendar/Conflict"), (keep.RedirectPath(), adopt.RedirectPath()));
        Assert.Equal(((ConflictId, 6), (ConflictId, 6)), (Assert.Single(app.Conflicts.Kept), Assert.Single(app.Conflicts.Adopted)));
        Assert.Single(app.OpLogs.Written, l => l.Action == "保持锁定" && l.PermCode == "calendar:conflict:keep");
        Assert.Single(app.OpLogs.Written, l => l.Action == "解锁并采用采集值" && l.PermCode == "calendar:conflict:adopt");
    }

    private static async Task<HttpClient> SignedInAsync(CrmApp app, long userId, params string[] permCodes)
    {
        app.Auth.AddAccount(userId, SignInStep.VerifyTotp);
        app.Permissions.Grant(userId, permCodes);
        if (app.Conflicts.Rows.Count == 0)
        {
            var lockedAt = new DateTime(2026, 11, 6, 14, 0, 0, DateTimeKind.Utc);
            var calendarEvent = new CalendarEvent
            {
                Id = 9, IndicatorCode = "US.CPI.MOM", ReferencePeriod = "2026-10", Status = CalendarEventStatus.Released, TimeZone = "America/New_York",
                TimePrecision = TimePrecision.Minute, Actual = 0.4m, RowVersion = 6,
                LockedFields = [new EventFieldLock { Field = EventField.Actual, LockedBy = "editor", LockedAt = lockedAt, Reason = "人工录入" }],
            };
            var conflict = new LockConflict
            {
                Id = ConflictId, EventId = 9, Field = EventField.Actual, LockedValue = "\"0.4\"", CollectedValue = "\"0.3\"", SourceId = 2, RunId = 1,
                FirstDetectedAt = lockedAt.AddHours(1), LastDetectedAt = lockedAt.AddHours(1),
            };
            app.Conflicts.Rows.Add(new LockConflictRow(
                conflict, calendarEvent, new BilingualText("CPI 月率", "CPI MoM"), IndicatorValueType.Value, IndicatorUnit.Percent, 1, "BLS API"));
        }

        return await SignedInBrowserAsync(app, $"user{userId}");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
