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
/// 事件详情（日历设计 §9.3）：显示锁定人和锁定时间（FR-009 验收第 2 条）、来源快照、改期历史（FR-007 验收第 3 条）；
/// 外部来的原始字符串转义输出（硬性规则 10）。日历事件服务用替身，查询和详情的数据库行为在 Suanpan.Services.Tests 中测试
/// </summary>
public class EventControllerTests
{
    private const long EventId = 8;

    [Fact]
    public async Task Detail_ShowsLockSnapshotAndReschedule()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, "calendar:event:detail", "collect:snapshot:detail");

        var html = System.Net.WebUtility.HtmlDecode(await browser.GetStringAsync($"/Calendar/Event/Detail/{EventId}", TestContext.Current.CancellationToken));

        Assert.Contains("editor1 锁定于", html);
        Assert.Contains("官方补发", html);
        Assert.Contains("href=\"/Collect/Snapshot/Detail/55\"", html);
        Assert.Contains("当地 2026-11-06 08:30（精确到分钟）", html);
        Assert.Contains("当地 2026-11-13 08:30（精确到分钟）", html);
    }

    [Fact]
    public async Task Detail_EscapesRawStrings()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, "calendar:event:detail");

        var html = await browser.GetStringAsync($"/Calendar/Event/Detail/{EventId}", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<b>raw</b>", html);
        Assert.DoesNotContain("href=\"/Collect/Snapshot/Detail/55\"", html);
    }

    private static async Task<HttpClient> SignedInAsync(CrmApp app, params string[] permCodes)
    {
        app.Auth.AddAccount(23, SignInStep.VerifyTotp);
        app.Permissions.Grant(23, permCodes);
        var lockedAt = new DateTime(2026, 11, 6, 14, 0, 0, DateTimeKind.Utc);
        var calendarEvent = new CalendarEvent
        {
            Id = EventId, IndicatorCode = "US.NFP", ReferencePeriod = "2026-10", Status = CalendarEventStatus.Released, Origin = EventOrigin.Collected,
            TimeZone = "America/New_York", ScheduledAt = new DateTime(2026, 11, 13, 13, 30, 0, DateTimeKind.Utc),
            ScheduledLocal = new DateTime(2026, 11, 13, 8, 30, 0), TimePrecision = TimePrecision.Minute,
            Actual = 119m, ActualSourceId = 2, SourceSnapshotId = 55, ActualRaw = "<b>raw</b>", DetectedAt = lockedAt,
            LockedFields = [new EventFieldLock { Field = EventField.Actual, LockedBy = "editor1", LockedAt = lockedAt, Reason = "官方补发" }],
        };
        var indicator = new Indicator
        {
            Code = "US.NFP", EconomyCode = "US", CurrencyCode = "USD", Name = new BilingualText("非农就业人数变化", "Nonfarm Payrolls"),
            ShortName = new BilingualText("非农", "NFP"), Agency = new BilingualText("劳工统计局", "BLS"),
            ValueType = IndicatorValueType.Value, Unit = IndicatorUnit.ThousandPersons, Decimals = 0, Importance = Importance.High,
        };
        var reschedule = new CalendarEventChange
        {
            EventId = EventId, Kind = EventChangeKind.Rescheduled, ActorKind = ChangeActorKind.Collector, SourceId = 1, ChangedBy = "collector",
            ChangedAt = lockedAt.AddDays(-7),
            OldValue = """{"at":"2026-11-06T13:30:00Z","local":"2026-11-06T08:30:00","precision":1}""",
            NewValue = """{"at":"2026-11-13T13:30:00Z","local":"2026-11-13T08:30:00","precision":1}""",
            Reason = "官方改期",
        };
        app.CalendarEvents.Details[EventId] = new EventDetail(
            calendarEvent, indicator, [], [], [reschedule], new Dictionary<long, string> { [1] = "BLS 发布日程", [2] = "BLS API" });
        return await SignedInBrowserAsync(app, "user23");
    }
}
