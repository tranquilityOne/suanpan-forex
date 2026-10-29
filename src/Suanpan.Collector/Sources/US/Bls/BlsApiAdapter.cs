using System.Net.Http.Json;
using Suanpan.Collector.Execution;

namespace Suanpan.Collector.Sources.US.Bls;

/// <summary>
/// BLS Public Data API v2 的适配器（数据源"BLS API"，官方 API，基础地址 https://api.bls.gov/publicAPI/v2/）。
/// 任务参数 <c>{"series": ["CES0000000001:chg", "LNS14000000"]}</c>：去掉计算方式、去重后，每 50 个序列 POST 一次
/// <c>{基础地址}timeseries/data/</c>，请求体为 <c>{"seriesid": [...], "registrationkey": "凭据"}</c>，不指定年份时 API 返回最近 3 年。
/// 数据源的凭据是 API Key（https://data.bls.gov/registrationEngine/ 申请，每年续期），没有凭据时按不注册的限额请求。
/// 快照的请求标识列出序列、不含 Key（采集设计 §5.5）
/// </summary>
public sealed class BlsApiAdapter : ICollectAdapter
{
    public const string AdapterKey = "us.blsapi";

    /// <summary>注册用户每次请求最多 50 个序列（API v2 的限额）</summary>
    public const int MaxSeriesPerRequest = 50;

    public string Key => AdapterKey;

    public async Task<FetchResult> FetchAsync(FetchContext context, CancellationToken ct)
    {
        var seriesIds = BlsParameters.Series(context.Parameters).Select(s => s.SeriesId).Distinct(StringComparer.Ordinal).ToList();
        var url = new Uri(BlsParameters.BaseUri(context.Source.BaseUrl), "timeseries/data/");
        var responses = new List<FetchedResponse>();
        foreach (var chunk in seriesIds.Chunk(MaxSeriesPerRequest))
        {
            var body = new Dictionary<string, object> { ["seriesid"] = chunk };
            if (!string.IsNullOrEmpty(context.Credential))
            {
                body["registrationkey"] = context.Credential;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            var response = await context.SendAsync(request, ct);
            responses.Add(response with { RequestKey = $"POST {url.AbsolutePath} series={string.Join(',', chunk)}" });
            if (!response.IsSuccess)
            {
                break;
            }
        }

        return new FetchResult(responses);
    }
}
