namespace Suanpan.Infrastructure.Redis;

/// <summary>
/// 分布式限流，多个实例共享同一份配额；用来控制每个数据源的请求频率（PRD BR-003）。
/// 算法为固定窗口：窗口内计数加 1 后不超过上限即放行，窗口交界处最多出现 2 倍突发。
/// Redis 异常时放行并记 Error 日志：限流是流量控制，可用性优先（docs/conventions/backend.md §8.1）。
/// </summary>
public interface IRateLimiter
{
    /// <summary>尝试取得一次配额。limit 小于等于 0 表示不限流，直接放行；window 一般为 1 秒</summary>
    Task<bool> TryAcquireAsync(string key, int limit, TimeSpan window);
}
