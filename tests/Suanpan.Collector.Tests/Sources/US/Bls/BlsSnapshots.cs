using System.Text;
using Suanpan.Collector.Execution;

namespace Suanpan.Collector.Tests.Sources.US.Bls;

/// <summary>
/// BLS 的真实快照样例（Snapshots/，2026-10-29 取得）：empsit、cpi、ppi、jolts 四个日程页，
/// 以及 API v2 对 9 个序列的一次响应 timeseries-data.json（不带 Key 请求，内容格式与带 Key 相同，含 2025 年停摆期间值为 "-" 的月份）
/// </summary>
internal static class BlsSnapshots
{
    public static FetchedResponse SchedulePage(string release)
        => new(new Uri($"https://www.bls.gov/schedule/news_release/{release}.htm"), 200, "text/html", Read($"{release}.htm"));

    public static FetchedResponse ApiResponse()
        => new(new Uri("https://api.bls.gov/publicAPI/v2/timeseries/data/"), 200, "application/json", Read("timeseries-data.json"));

    /// <summary>不是真实快照的内容，用于真实快照中没有出现的分支</summary>
    public static FetchedResponse Inline(string url, string content, string contentType = "text/html")
        => new(new Uri(url), 200, contentType, Encoding.UTF8.GetBytes(content));

    private static byte[] Read(string file) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sources", "US", "Bls", "Snapshots", file));
}
