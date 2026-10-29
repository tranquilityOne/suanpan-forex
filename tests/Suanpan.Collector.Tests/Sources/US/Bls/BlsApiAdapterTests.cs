using System.Net;
using System.Text.Json;
using Suanpan.Collector.Execution;
using Suanpan.Collector.Sources.US.Bls;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Infrastructure.Redis;
using Xunit;

namespace Suanpan.Collector.Tests.Sources.US.Bls;

/// <summary>BLS API 的请求（BR-004、采集设计 §5.5）：Key 只放在请求体中，快照的请求标识只列序列</summary>
public class BlsApiAdapterTests
{
    private const string ApiKey = "test-registration-key";

    /// <summary>外部标识去掉计算方式、去重后一次 POST；请求体带 Key，请求标识不带</summary>
    [Fact]
    public async Task Fetch_PostsSeriesWithKeyOutsideRequestKey()
    {
        var handler = new RecordingHandler();
        var context = new FetchContext(
            new DataSource { Id = 1, BaseUrl = "https://api.bls.gov/publicAPI/v2", RateLimit = 1, RateLimitUnit = RateLimitUnit.PerSecond, TimeoutSeconds = 30 },
            ApiKey, """{"series": ["CUSR0000SA0:pct", "CUUR0000SA0:pct12", "CUSR0000SA0", "LNS14000000"]}""",
            new HttpClient(handler), new UnlimitedRateLimiter(), TimeProvider.System);

        var fetched = await new BlsApiAdapter().FetchAsync(context, TestContext.Current.CancellationToken);

        var (url, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://api.bls.gov/publicAPI/v2/timeseries/data/", url.ToString());
        Assert.Equal(["CUSR0000SA0", "CUUR0000SA0", "LNS14000000"], body.RootElement.GetProperty("seriesid").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(ApiKey, body.RootElement.GetProperty("registrationkey").GetString());
        var requestKey = Assert.Single(fetched.Responses).RequestKey;
        Assert.Equal("POST /publicAPI/v2/timeseries/data/ series=CUSR0000SA0,CUUR0000SA0,LNS14000000", requestKey);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(Uri Url, JsonDocument Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!, JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct))));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }

    private sealed class UnlimitedRateLimiter : IRateLimiter
    {
        public Task<bool> TryAcquireAsync(string key, int limit, TimeSpan window) => Task.FromResult(true);
    }
}
