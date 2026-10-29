using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Reference.Entities;
using Suanpan.Data;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Collect.Sources.Models;
using Suanpan.Services.Reference.Economies;
using Suanpan.Services.Reference.Economies.Models;
using Suanpan.Services.Sys.Security;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>
/// 数据源的保存、凭据、启用、连通性测试（采集设计 §3.1，FR-001）。字段规则的各个分支在 Core 的 DataSourceRuleTests 中测试，
/// 这里测服务接上了规则、凭据加密，以及需要查库的校验
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class DataSourceServiceTests(PostgresFixture postgres)
{
    private const string Key = "bls-api-key-0001";

    private readonly AesSecretProtector _protector = new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) })
        .Build());

    private readonly StubHttp _http = new();
    private readonly StubRateLimiter _rateLimiter = new();

    /// <summary>FR-001 验收第 1、2 条：新建为停用；凭据在库中为密文，能解密回原值</summary>
    [Fact]
    public async Task Create_SavesDisabledWithEncryptedCredential()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var name = TestData.Unique("源");

        var result = await Create(db).CreateAsync(Input(name) with { Credential = $" {Key} " }, Ct);

        Assert.True(result.Success, result.Error);
        var saved = await db.DataSources.AsNoTracking().SingleAsync(s => s.Name == name, Ct);
        Assert.False(saved.IsEnabled);
        Assert.StartsWith("enc:", saved.Credential);
        Assert.Equal(Key, _protector.Decrypt(saved.Credential!));
    }

    [Fact]
    public async Task Update_KeepsCredentialWhenBlank_AndClearsOnRequest()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var name = TestData.Unique("源");
        await service.CreateAsync(Input(name) with { CredentialRequired = false }, Ct);
        var id = await IdOf(db, name);
        var original = await CredentialOf(db, id);

        var kept = await service.UpdateAsync(id, Input(name) with { CredentialRequired = false, Credential = "  ", Agency = "改过的机构" }, Ct);
        var afterKeep = await CredentialOf(db, id);
        var cleared = await service.UpdateAsync(id, Input(name) with { CredentialRequired = false, ClearCredential = true }, Ct);

        Assert.True(kept.Success && cleared.Success);
        Assert.NotNull(original);
        Assert.Equal(original, afterKeep);
        Assert.Null(await CredentialOf(db, id));
    }

    /// <summary>名称重复、选用停用的经济体、需要凭据却没有（ERR-SRC-004），都在对应的字段提示</summary>
    [Fact]
    public async Task Create_RejectsDuplicateName_DisabledEconomy_AndMissingCredential()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var name = TestData.Unique("源");
        await service.CreateAsync(Input(name), Ct);
        var economies = new EconomyService(db);
        await economies.CreateAsync(new EconomyInput("QK", "测试国", "Testland", "USD", "America/New_York", 900), Ct);
        await economies.SetEnabledAsync("QK", false, Ct);

        var duplicate = await service.CreateAsync(Input(name), Ct);
        var disabledEconomy = await service.CreateAsync(Input(TestData.Unique("源")) with { EconomyCode = "qk" }, Ct);
        var noCredential = await service.CreateAsync(Input(TestData.Unique("源")) with { Credential = null }, Ct);

        Assert.Equal((false, nameof(DataSourceInput.Name)), (duplicate.Success, duplicate.Field));
        Assert.Equal((false, nameof(DataSourceInput.EconomyCode)), (disabledEconomy.Success, disabledEconomy.Field));
        Assert.Equal((false, nameof(DataSourceInput.Credential)), (noCredential.Success, noCredential.Field));
    }

    /// <summary>FR-001 验收第 1 条、BR-002：合规类别为"待确认"时不能启用；确认后可以</summary>
    [Fact]
    public async Task SetEnabled_RequiresConfirmedCompliance()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var name = TestData.Unique("源");
        await service.CreateAsync(Input(name) with { ComplianceClass = ComplianceClass.Pending }, Ct);
        var id = await IdOf(db, name);

        var rejected = await service.SetEnabledAsync(id, true, Ct);
        await service.UpdateAsync(id, Input(name) with { ComplianceClass = ComplianceClass.GovernmentPublic }, Ct);
        var accepted = await service.SetEnabledAsync(id, true, Ct);

        Assert.False(rejected.Success);
        Assert.True(accepted.Success, accepted.Error);
        Assert.True((await service.GetAsync(id, Ct))!.IsEnabled);
    }

    /// <summary>FR-001 验收第 3 条、BR-006、ERR-SRC-003：被采集任务引用时不能删除，提示中列出任务</summary>
    [Fact]
    public async Task Delete_RejectsSourceUsedByTask()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var name = TestData.Unique("源");
        await service.CreateAsync(Input(name), Ct);
        var id = await IdOf(db, name);
        var task = TestData.Unique("任务");
        db.CollectTasks.Add(new CollectTask { Name = task, SourceId = id, ParserKey = "us.test.schedule", TargetType = CollectTargetType.Calendar });
        await db.SaveChangesAsync(Ct);

        var result = await service.DeleteAsync(id, Ct);

        Assert.False(result.Success);
        Assert.Contains(task, result.Error);
        Assert.NotNull(await service.GetAsync(id, Ct));
    }

    /// <summary>BR-006、ERR-SRC-003：被指标映射引用时不能删除，提示中列出指标（BR-034）</summary>
    [Fact]
    public async Task Delete_RejectsSourceMappedToIndicator()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var name = TestData.Unique("源");
        await service.CreateAsync(Input(name), Ct);
        var id = await IdOf(db, name);
        var code = TestData.IndicatorCode();
        db.IndicatorSources.Add(new IndicatorSource { IndicatorCode = code, SourceId = id, ExternalId = "S1", ParserKey = "us.test.release", Priority = 1 });
        await db.SaveChangesAsync(Ct);

        var result = await service.DeleteAsync(id, Ct);

        Assert.False(result.Success);
        Assert.Contains(code, result.Error);
        Assert.NotNull(await service.GetAsync(id, Ct));
    }

    /// <summary>
    /// BR-006、ERR-SRC-003：被日历引用时不能删除（日历设计 §7）——事件的排期或公布值来自它、有它的数值或锁定冲突，
    /// 或者它的日程中有未匹配、已忽略的发布标识；提示中列出事件或发布标识
    /// </summary>
    [Theory]
    [InlineData("schedule")]
    [InlineData("actual")]
    [InlineData("value")]
    [InlineData("conflict")]
    [InlineData("unmatched")]
    [InlineData("ignored")]
    public async Task Delete_RejectsSourceReferencedByCalendar(string reference)
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var name = TestData.Unique("源");
        await service.CreateAsync(Input(name), Ct);
        var id = await IdOf(db, name);
        var code = TestData.IndicatorCode();
        var calendarEvent = TestData.Event(code);
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(Ct);
        var expected = $"{code} {calendarEvent.ReferencePeriod}";
        var at = new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc);
        switch (reference)
        {
            case "schedule":
                calendarEvent.ScheduleSourceId = id;
                break;
            case "actual":
                calendarEvent.ActualSourceId = id;
                break;
            case "value":
                db.CalendarEventValues.Add(new CalendarEventValue { EventId = calendarEvent.Id, SourceId = id, Value = 1, Raw = "1", RunId = 1, DetectedAt = at });
                break;
            case "conflict":
                db.LockConflicts.Add(new LockConflict
                {
                    EventId = calendarEvent.Id, Field = EventField.Actual, LockedValue = "\"1\"", CollectedValue = "\"2\"", SourceId = id, RunId = 1,
                    FirstDetectedAt = at, LastDetectedAt = at,
                });
                break;
            case "unmatched":
                expected = TestData.Unique("rel");
                db.UnmatchedEntries.Add(new UnmatchedEntry
                {
                    SourceId = id, ReleaseKey = expected, ReleaseName = "Productivity and Costs", ReferencePeriod = "2026-Q3",
                    TimePrecision = TimePrecision.Minute, FirstSeenAt = at, LastSeenAt = at,
                });
                break;
            case "ignored":
                expected = TestData.Unique("rel");
                db.IgnoredReleases.Add(new IgnoredRelease { SourceId = id, ReleaseKey = expected, ReleaseName = "Real Earnings" });
                break;
        }

        await db.SaveChangesAsync(Ct);

        var result = await service.DeleteAsync(id, Ct);

        Assert.False(result.Success);
        Assert.Contains(expected, result.Error);
        Assert.NotNull(await service.GetAsync(id, Ct));
    }

    [Fact]
    public async Task Delete_RemovesUnreferencedSource()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var name = TestData.Unique("源");
        await service.CreateAsync(Input(name), Ct);
        var id = await IdOf(db, name);

        var result = await service.DeleteAsync(id, Ct);

        Assert.True(result.Success, result.Error);
        Assert.Null(await service.GetAsync(id, Ct));
    }

    /// <summary>连通性测试返回状态码和响应开头；非 2xx 算失败（ERR-SRC-002）</summary>
    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task Test_ReportsStatusAndSnippet(HttpStatusCode status, bool success)
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var name = TestData.Unique("源");
        await service.CreateAsync(Input(name), Ct);
        _http.Respond(status, new string('数', DataSourceService.SnippetLength + 100));

        var result = await service.TestAsync(await IdOf(db, name), Ct);

        Assert.Equal((success, (int)status), (result.Success, result.StatusCode));
        Assert.Equal(DataSourceService.SnippetLength, result.Snippet!.Length);
        Assert.Equal(["https://api.example.org/v2/"], _http.Requested);
    }

    /// <summary>robots.txt 为"不允许"、或已达到频率上限（BR-003）时不发请求</summary>
    [Theory]
    [InlineData(RobotsResult.Disallowed, true)]
    [InlineData(RobotsResult.Allowed, false)]
    public async Task Test_SendsNothingWhenDisallowedOrThrottled(RobotsResult robots, bool rateLimiterAllows)
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var name = TestData.Unique("源");
        await service.CreateAsync(Input(name) with { RobotsResult = robots }, Ct);
        _rateLimiter.Allow = rateLimiterAllows;

        var result = await service.TestAsync(await IdOf(db, name), Ct);

        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
        Assert.Empty(_http.Requested);
    }

    private DataSourceService Create(SuanpanDbContext db) => new(db, _protector, _http, _rateLimiter, TimeProvider.System);

    private static DataSourceInput Input(string name) => new()
    {
        Name = name,
        Agency = "测试机构",
        EconomyCode = "US",
        Type = DataSourceType.OfficialApi,
        BaseUrl = "https://api.example.org/v2/",
        RateLimit = 2,
        RateLimitUnit = RateLimitUnit.PerSecond,
        TimeoutSeconds = 30,
        CredentialRequired = true,
        Credential = Key,
        ComplianceClass = ComplianceClass.GovernmentPublic,
        TermsUrl = "https://www.example.org/terms",
        RobotsResult = RobotsResult.NotApplicable,
    };

    private static Task<long> IdOf(SuanpanDbContext db, string name)
        => db.DataSources.Where(s => s.Name == name).Select(s => s.Id).SingleAsync(Ct);

    private static Task<string?> CredentialOf(SuanpanDbContext db, long id)
        => db.DataSources.AsNoTracking().Where(s => s.Id == id).Select(s => s.Credential).SingleAsync(Ct);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>返回预设的响应，记下请求过的地址</summary>
    private sealed class StubHttp : HttpMessageHandler, IHttpClientFactory
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = string.Empty;

        public List<string> Requested { get; } = [];

        public void Respond(HttpStatusCode status, string body) => (_status, _body) = (status, body);

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }
    }

    private sealed class StubRateLimiter : IRateLimiter
    {
        public bool Allow { get; set; } = true;

        public Task<bool> TryAcquireAsync(string key, int limit, TimeSpan window) => Task.FromResult(Allow);
    }
}
