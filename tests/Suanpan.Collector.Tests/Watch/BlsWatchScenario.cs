using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Suanpan.Collector.Tests.Fixtures;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Services.Collect.Watch;
using Suanpan.Services.Collect.Watch.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Reference.Indicators.Models;

namespace Suanpan.Collector.Tests.Watch;

/// <summary>
/// 盯盘的回放场景（FR-003 验收第 6 条）：指向 StubHttp 的"BLS API"数据源和它的公布值任务（us.blsapi.release），
/// 非农、失业率、平均时薪三个高重要性指标，以及它们 2026-09 的事件（排期为 at）。数据源先返回公布前的内容
/// （真实快照 timeseries-data.json 去掉最新一期），<see cref="Release()"/> 之后返回真实快照；<see cref="Release(int)"/>
/// 只发布前几个序列（模拟同一份报告中部分指标缺失）。
/// 各测试用不同的排期时间，盯盘计划互不影响
/// </summary>
internal sealed class BlsWatchScenario
{
    public const string Period = "2026-09";

    private const string ParserKey = "us.blsapi.release";

    private static readonly (string Spec, IndicatorUnit Unit, short Decimals)[] Series =
    [
        ("CES0000000001:chg", IndicatorUnit.ThousandPersons, 0),
        ("LNS14000000", IndicatorUnit.Percent, 1),
        ("CES0500000003:pct", IndicatorUnit.Percent, 1),
    ];

    private static readonly byte[] After = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sources", "US", "Bls", "Snapshots", "timeseries-data.json"));

    private volatile int _released;

    private BlsWatchScenario(DateTime at) => ScheduledAt = at;

    public DateTime ScheduledAt { get; }

    public string Host { get; private set; } = string.Empty;

    public DataSource Source { get; private set; } = null!;

    public CollectTask Task { get; private set; } = null!;

    public List<CalendarEvent> Events { get; } = [];

    /// <summary>不为空时数据源一律返回这个状态码（模拟被限流）</summary>
    public HttpStatusCode? Status { get; set; }

    /// <summary>官方发布：之后返回真实快照</summary>
    public void Release() => _released = Series.Length;

    /// <summary>只发布前 count 个序列，其余仍是上一期</summary>
    public void Release(int count) => _released = count;

    public static async Task<BlsWatchScenario> CreateAsync(CollectorFixture fixture, CollectorApp app, DateTime at, short? intervalSeconds = null)
    {
        var scenario = new BlsWatchScenario(at);
        scenario.Host = fixture.Http.NewHost((_, _) => System.Threading.Tasks.Task.FromResult(scenario.Respond()));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SuanpanDbContext>();
        var indicators = scope.ServiceProvider.GetRequiredService<IIndicatorService>();

        scenario.Source = new DataSource
        {
            Name = $"BLS API {Guid.NewGuid():N}",
            Agency = "美国劳工统计局",
            Type = DataSourceType.OfficialApi,
            BaseUrl = $"https://{scenario.Host}/publicAPI/v2/",
            RateLimit = 100,
            RateLimitUnit = RateLimitUnit.PerSecond,
            WatchIntervalSeconds = intervalSeconds,
            ComplianceClass = ComplianceClass.GovernmentPublic,
            TermsUrl = $"https://{scenario.Host}/terms",
            RobotsResult = RobotsResult.NotApplicable,
            IsEnabled = true,
        };
        db.DataSources.Add(scenario.Source);
        await db.SaveChangesAsync();

        scenario.Task = new CollectTask
        {
            Name = $"BLS 公布值 {Guid.NewGuid():N}",
            SourceId = scenario.Source.Id,
            ParserKey = ParserKey,
            TargetType = CollectTargetType.Release,
            Params = new JsonObject { ["series"] = new JsonArray(Series.Select(s => (JsonNode)s.Spec).ToArray()) }.ToJsonString(),
            IsEnabled = true,
        };
        db.CollectTasks.Add(scenario.Task);

        foreach (var (spec, unit, decimals) in Series)
        {
            var code = $"US.W{Guid.NewGuid():N}"[..12].ToUpperInvariant();
            var created = await indicators.CreateAsync(Indicator(code, unit, decimals));
            if (!created.Success)
            {
                throw new InvalidOperationException($"建指标失败：{created.Error}");
            }

            db.IndicatorSources.Add(new IndicatorSource
            {
                IndicatorCode = code, SourceId = scenario.Source.Id, Purpose = IndicatorSourcePurpose.Release, ExternalId = spec, ParserKey = ParserKey,
                Priority = 1,
            });
            var calendarEvent = new CalendarEvent
            {
                IndicatorCode = code, ReferencePeriod = Period, Origin = EventOrigin.Collected, TimeZone = "America/New_York",
                ScheduledLocal = DateTime.SpecifyKind(at.AddHours(-5), DateTimeKind.Unspecified), ScheduledAt = at, TimePrecision = TimePrecision.Minute,
            };
            db.CalendarEvents.Add(calendarEvent);
            scenario.Events.Add(calendarEvent);
        }

        await db.SaveChangesAsync();
        return scenario;
    }

    /// <summary>盯盘计划中这个数据源在排期时间的那一路</summary>
    public async Task<WatchGroup> GroupAsync(CollectorApp app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var plan = await scope.ServiceProvider.GetRequiredService<IWatchPlanService>().GetPlanAsync(ScheduledAt.AddHours(-1), ScheduledAt.AddHours(1));
        return plan.Groups.Single(g => g.SourceId == Source.Id && g.ScheduledAt == ScheduledAt);
    }

    public async Task<List<CalendarEvent>> EventsAsync(CollectorApp app)
    {
        var ids = Events.Select(e => e.Id).ToList();
        List<CalendarEvent> events = [];
        await app.ExecuteAsync(async db => events = await db.CalendarEvents.AsNoTracking().Where(e => ids.Contains(e.Id)).OrderBy(e => e.Id).ToListAsync());
        return events;
    }

    private HttpResponseMessage Respond()
        => Status is { } status
            ? StubHttp.Reply(status, "Too Many Requests")
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_released == Series.Length ? After : WithoutLatest(After, Series.Take(_released).Select(s => s.Spec.Split(':')[0]))),
            };

    private static IndicatorInput Indicator(string code, IndicatorUnit unit, short decimals) => new()
    {
        Code = code,
        EconomyCode = "US",
        CurrencyCode = "USD",
        NameZh = "盯盘测试指标",
        NameEn = "Watch Test Indicator",
        ShortNameZh = "盯盘测试",
        ShortNameEn = "Watch Test",
        Category = IndicatorCategory.Other,
        ValueType = IndicatorValueType.Value,
        Unit = unit,
        Decimals = decimals,
        Frequency = ReleaseFrequency.Monthly,
        Importance = Importance.High,
        Direction = ImpactDirection.HigherIsBullish,
        FlatTolerance = 0,
        AgencyZh = "测试机构",
        AgencyEn = "Test Agency",
        DefaultReleaseTime = new TimeOnly(8, 30),
    };

    /// <summary>公布前的内容：released 以外的序列去掉最新的一期（API 按时间倒序返回，第一项就是最新的），下一项标为最新</summary>
    private static byte[] WithoutLatest(byte[] after, IEnumerable<string> released)
    {
        var keep = released.ToHashSet(StringComparer.Ordinal);
        var root = JsonNode.Parse(after)!;
        foreach (var series in root["Results"]!["series"]!.AsArray().Where(s => !keep.Contains((string)s!["seriesID"]!)))
        {
            var data = series!["data"]!.AsArray();
            data.RemoveAt(0);
            if (data.Count > 0)
            {
                data[0]!["latest"] = "true";
            }
        }

        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }
}
