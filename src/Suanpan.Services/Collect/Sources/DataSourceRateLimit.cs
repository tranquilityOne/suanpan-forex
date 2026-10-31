using System.Globalization;

namespace Suanpan.Services.Collect.Sources;

/// <summary>
/// 数据源请求频率上限的限流键（BR-003，采集设计 §4.3）：连通性测试、采集任务、盯盘共用同一个键，合计不超过上限；
/// 窗口见 DataSourceRule.RateLimitWindow。每日上限按 UTC 日期另用一个计数键（<see cref="IDataSourceDailyQuota"/>）
/// </summary>
public static class DataSourceRateLimit
{
    public static string Key(long sourceId) => $"collect:source:{sourceId}";

    public static string DailyKey(long sourceId, DateTime utcNow)
        => $"collect:source:{sourceId}:day:{utcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";
}
