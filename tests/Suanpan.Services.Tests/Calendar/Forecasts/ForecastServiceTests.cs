using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Forecasts;
using Suanpan.Services.Calendar.Forecasts.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Configs.Models;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Mvc;
using Xunit;

namespace Suanpan.Services.Tests.Calendar.Forecasts;

/// <summary>
/// 预期值的录入和更正（日历设计 §3.8，FR-010）。"公布后"的判断在 Core 的 ForecastRuleTests、数值格式在 EventValueRuleTests 中测试，
/// 这里测服务接上了规则以及数据库中的结果。指标为数值型、百分比、1 位小数；断言只看本测试建的指标和事件
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class ForecastServiceTests(PostgresFixture postgres)
{
    /// <summary>TestData.Event 的排期为 2026-11-06 13:30 UTC，在"现在"之后的第 1 天</summary>
    private static readonly DateTime Now = new(2026, 11, 5, 14, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// BR-055：清单只列未来 7 天内、未公布、没有预期值的高重要性事件，按排期升序；切换为全部重要性时列出中低重要性的
    /// </summary>
    [Fact]
    public async Task QueryPending_ListsUpcomingHighImportanceWithoutForecast()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var high = await AddIndicatorAsync(db, Importance.High);
        var medium = await AddIndicatorAsync(db, Importance.Medium);
        var due = TestData.Event(high, "2026-10");
        var later = At(TestData.Event(high, "2026-11"), Now.AddDays(6));
        var entered = TestData.Event(high, "2026-09");
        entered.Forecast = 0.3m;
        var released = TestData.Event(high, "2026-08");
        released.Status = CalendarEventStatus.Released;
        var beyond = At(TestData.Event(high, "2026-12"), Now.AddDays(8));
        var past = At(TestData.Event(high, "2026-07"), Now.AddHours(-1));
        var mediumDue = TestData.Event(medium);
        db.CalendarEvents.AddRange(later, due, entered, released, beyond, past, mediumDue);
        await db.SaveChangesAsync(Ct);
        var service = Create(db);

        var highOnly = await service.QueryPendingAsync(new ForecastQuery(PageSize: 500), Ct);
        var all = await service.QueryPendingAsync(new ForecastQuery(AllImportance: true, PageSize: 500), Ct);

        long[] Ours(IEnumerable<long> ids) => ids.Where(id => new[] { due.Id, later.Id, entered.Id, released.Id, beyond.Id, past.Id, mediumDue.Id }.Contains(id)).ToArray();
        Assert.Equal([due.Id, later.Id], Ours(highOnly.Items.Select(r => r.Event.Id)));
        Assert.Equal([due.Id, mediumDue.Id, later.Id], Ours(all.Items.Select(r => r.Event.Id)));
    }

    /// <summary>
    /// ERR-FC-001、BR-054、ERR-FC-003：格式按指标、来源说明不超过 100 字、row_version 不对时拒绝；
    /// 保存后写预期值、来源类型和说明，锁定预期值（BR-061），写一条人工的变更历史；再次提交同样的值不再写历史
    /// </summary>
    [Fact]
    public async Task Save_ValidatesThenLocksAndRecordsHistory()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var calendarEvent = TestData.Event(await AddIndicatorAsync(db, Importance.High));
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(Ct);
        var service = Create(db);
        var version = calendarEvent.RowVersion;

        var badFormat = await service.SaveAsync(calendarEvent.Id, new ForecastInput(0.15m, null, null), version, Ct);
        var longNote = await service.SaveAsync(calendarEvent.Id, new ForecastInput(0.3m, null, new string('路', ForecastRule.MaxNoteLength + 1)), version, Ct);
        var stale = await service.SaveAsync(calendarEvent.Id, new ForecastInput(0.3m, null, null), version - 1, Ct);
        var saved = await service.SaveAsync(calendarEvent.Id, new ForecastInput(0.3m, null, " 路透调查 "), version, Ct);
        var again = await service.SaveAsync(calendarEvent.Id, new ForecastInput(0.3m, null, "路透调查"), version + 1, Ct);

        Assert.Equal(("Forecast", "Note", false, true, true), (badFormat.Field, longNote.Field, stale.Success, saved.Success, again.Success));
        var stored = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == calendarEvent.Id, Ct);
        Assert.Equal(((decimal?)0.3m, (ForecastType?)ForecastType.Manual, "路透调查", version + 1),
            (stored.Forecast, stored.ForecastType, stored.ForecastNote, stored.RowVersion));
        Assert.Equal((EventField.Forecast, "editor"), (Assert.Single(stored.LockedFields).Field, stored.LockedFields[0].LockedBy));
        var change = await db.CalendarEventChanges.AsNoTracking().SingleAsync(c => c.EventId == calendarEvent.Id, Ct);
        Assert.Equal((EventChangeKind.Forecast, ChangeActorKind.User), (change.Kind, change.ActorKind));
    }

    /// <summary>
    /// BR-057：事件公布后不能再录入（ERR-FC-002）；管理员更正时原因必填，原因写进变更历史和锁定信息；没有改动时拒绝
    /// </summary>
    [Fact]
    public async Task Released_RejectsSave_CorrectRequiresReason()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var released = TestData.Event(await AddIndicatorAsync(db, Importance.High));
        released.Status = CalendarEventStatus.Released;
        released.Actual = 0.4m;
        released.Forecast = 0.3m;
        db.CalendarEvents.Add(released);
        await db.SaveChangesAsync(Ct);
        var service = Create(db);
        var version = released.RowVersion;

        var save = await service.SaveAsync(released.Id, new ForecastInput(0.2m, null, null), version, Ct);
        var noReason = await service.CorrectAsync(released.Id, new ForecastInput(0.2m, null, null), " ", version, Ct);
        var unchanged = await service.CorrectAsync(released.Id, new ForecastInput(0.3m, null, null), "录入时填错", version, Ct);
        var corrected = await service.CorrectAsync(released.Id, new ForecastInput(0.2m, null, null), "录入时填错", version, Ct);

        Assert.Equal((false, "Reason", false, true), (save.Success, noReason.Field, unchanged.Success, corrected.Success));
        var stored = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == released.Id, Ct);
        Assert.Equal(((decimal?)0.2m, "录入时填错"), (stored.Forecast, Assert.Single(stored.LockedFields).Reason));
        var change = await db.CalendarEventChanges.AsNoTracking().SingleAsync(c => c.EventId == released.Id, Ct);
        Assert.Equal((EventChangeKind.Forecast, "录入时填错"), (change.Kind, change.Reason));
    }

    /// <summary>BR-054：常用来源按逗号分隔，中文逗号也认，去掉空项和重复项</summary>
    [Fact]
    public async Task NoteOptions_SplitByCommas()
    {
        await using var db = await postgres.CreateAppContextAsync();

        var options = await Create(db, " 路透调查, 彭博调查，, 路透调查 ").GetNoteOptionsAsync(Ct);

        Assert.Equal(["路透调查", "彭博调查"], options);
    }

    private static ForecastService Create(SuanpanDbContext db, string noteOptions = "")
        => new(db, new FixedAuditActorProvider("editor"), new StubConfigService(noteOptions), new FixedTimeProvider(new DateTimeOffset(Now)));

    private static async Task<string> AddIndicatorAsync(SuanpanDbContext db, Importance importance)
    {
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code) with { Importance = importance }, Ct);
        return code;
    }

    private static CalendarEvent At(CalendarEvent calendarEvent, DateTime scheduledAt)
    {
        calendarEvent.ScheduledAt = scheduledAt;
        return calendarEvent;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>只给出常用来源，其余配置取默认值；配置服务本身在 Sys 的测试中测试</summary>
    private sealed class StubConfigService(string noteOptions) : IConfigService
    {
        public Task<string?> GetStringAsync(string key, string? defaultValue, CancellationToken ct = default)
            => Task.FromResult(key == CalendarConfigKeys.ForecastNoteOptions ? noteOptions : defaultValue);

        public Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<IReadOnlyList<ConfigRow>> GetListAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ManageResult> SaveAsync(string key, string value, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ManageResult> ResetAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
