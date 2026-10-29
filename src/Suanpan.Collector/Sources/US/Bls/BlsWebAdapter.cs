using Suanpan.Collector.Execution;

namespace Suanpan.Collector.Sources.US.Bls;

/// <summary>
/// BLS 官网发布日程的适配器（数据源"BLS 发布日程"，官方网页，基础地址 https://www.bls.gov/schedule/news_release/）。
/// 任务参数 <c>{"releases": ["empsit", "cpi", "ppi", "jolts"]}</c>：按发布标识逐页 GET <c>{基础地址}{发布标识}.htm</c>，
/// 每页一个响应；某一页失败时不再请求后面的页（整次运行按失败处理）
/// </summary>
public sealed class BlsWebAdapter : ICollectAdapter
{
    public const string AdapterKey = "us.blsweb";

    public string Key => AdapterKey;

    public async Task<FetchResult> FetchAsync(FetchContext context, CancellationToken ct)
    {
        var releases = BlsParameters.Releases(context.Parameters);
        var baseUri = BlsParameters.BaseUri(context.Source.BaseUrl);
        var responses = new List<FetchedResponse>();
        foreach (var release in releases)
        {
            var response = await context.GetAsync(new Uri(baseUri, $"{release}.htm"), ct);
            responses.Add(response);
            if (!response.IsSuccess)
            {
                break;
            }
        }

        return new FetchResult(responses);
    }

    /// <summary>日程页地址中的发布标识：文件名去掉扩展名，如 empsit.htm → empsit</summary>
    public static string ReleaseKeyOf(Uri url) => Path.GetFileNameWithoutExtension(url.AbsolutePath).ToLowerInvariant();
}
