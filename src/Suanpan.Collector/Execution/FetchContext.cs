using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Rules;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Collect.Sources;

namespace Suanpan.Collector.Execution;

/// <summary>
/// 适配器发请求用的上下文（采集设计 §4.3 第 4 步、§5.1）：数据源配置、解密后的凭据、任务参数，以及受控的发送方法。
/// 每次请求前按数据源的频率上限申请配额（BR-003），拿不到就等到下一个窗口；超时取数据源的 timeout_seconds；
/// User-Agent 由采集用的 HttpClient 统一设置（BR-005）
/// </summary>
public sealed class FetchContext(
    DataSource source,
    string? credential,
    string? parameters,
    HttpClient client,
    IRateLimiter rateLimiter,
    TimeProvider timeProvider)
{
    public DataSource Source => source;

    /// <summary>解密后的凭据（如 API Key），只在发请求时使用，不能写进日志和异常信息（BR-004）</summary>
    public string? Credential => credential;

    /// <summary>任务参数（JSON 对象），具体字段由适配器和解析器约定</summary>
    public string? Parameters => parameters;

    /// <summary>本次执行因频率上限累计等待的毫秒数，写进请求记录（运行详情显示"已延后"）</summary>
    public int DeferredMs { get; private set; }

    public Task<FetchedResponse> GetAsync(Uri url, CancellationToken ct) => SendAsync(new HttpRequestMessage(HttpMethod.Get, url), ct);

    /// <summary>发出一个请求并读完响应。超时抛出 <see cref="FetchTimeoutException"/>，网络错误抛出 HttpRequestException；非 2xx 照常返回</summary>
    public async Task<FetchedResponse> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await WaitForQuotaAsync(ct);

        var timeout = TimeSpan.FromSeconds(source.TimeoutSeconds);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource.CancelAfter(timeout);
        try
        {
            using var response = await client.SendAsync(request, timeoutSource.Token);
            var body = await response.Content.ReadAsByteArrayAsync(timeoutSource.Token);
            return new FetchedResponse(request.RequestUri!, (int)response.StatusCode, response.Content.Headers.ContentType?.ToString(), body)
            {
                ReceivedAt = timeProvider.GetUtcNow().UtcDateTime,
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new FetchTimeoutException(timeout);
        }
    }

    /// <summary>
    /// 固定窗口限流（与 RedisRateLimiter 的窗口号算法一致）：拿不到配额时等到下一个窗口开始再申请；
    /// 所有任务和盯盘共用数据源的同一个键，合计不超过上限（ERR-COL-003）
    /// </summary>
    private async Task WaitForQuotaAsync(CancellationToken ct)
    {
        var window = DataSourceRule.RateLimitWindow(source.RateLimitUnit);
        var key = DataSourceRateLimit.Key(source.Id);
        while (!await rateLimiter.TryAcquireAsync(key, source.RateLimit, window))
        {
            var started = timeProvider.GetTimestamp();
            var untilNextWindow = TimeSpan.FromTicks(window.Ticks - timeProvider.GetUtcNow().UtcTicks % window.Ticks);
            await Task.Delay(untilNextWindow, timeProvider, ct);
            DeferredMs += (int)timeProvider.GetElapsedTime(started).TotalMilliseconds;
        }
    }
}

/// <summary>请求超过数据源的超时时间；可以重试（采集设计 §3.4）</summary>
public sealed class FetchTimeoutException(TimeSpan timeout) : Exception($"请求超时（{timeout.TotalSeconds:0} 秒）");
