using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Ops.Enums;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>
/// 数据源的每日请求上限（BR-003，采集设计 §4.3，ERR-COL-010）。阈值的算法在 Core 的 DataSourceRuleTests 中测试；
/// Redis 用内存替身，计数按 UTC 日期分键
/// </summary>
public class DataSourceDailyQuotaTests
{
    private static readonly DateTimeOffset Today = new(2026, 11, 6, 13, 30, 0, TimeSpan.Zero);

    /// <summary>上限 10：前 10 次放行，第 11 次拦下；第 9 次（90%）和第一次被拦下时各告警一次，之后不再重复</summary>
    [Fact]
    public async Task TryConsume_StopsAtTheLimitAndAlertsTwice()
    {
        var alerts = new RecordingAlertService();
        var quota = Create(new FakeRedisCache(), alerts, new FixedTimeProvider(Today));
        var source = Source(dailyLimit: 10);

        var results = new List<bool>();
        for (var i = 0; i < 12; i++)
        {
            results.Add(await quota.TryConsumeAsync(source, Ct));
        }

        bool[] expected = [.. Enumerable.Repeat(true, 10), false, false];
        Assert.Equal(expected, results);
        Assert.Equal([(AlertLevel.Important, source.Name), (AlertLevel.Important, source.Name)], alerts.Raised);
    }

    /// <summary>按 UTC 自然日计数：第二天重新开始</summary>
    [Fact]
    public async Task TryConsume_StartsOverTheNextUtcDay()
    {
        var time = new FixedTimeProvider(Today);
        var quota = Create(new FakeRedisCache(), new RecordingAlertService(), time);
        var source = Source(dailyLimit: 1);

        var first = await quota.TryConsumeAsync(source, Ct);
        var second = await quota.TryConsumeAsync(source, Ct);
        time.UtcNow = Today.AddDays(1);
        var nextDay = await quota.TryConsumeAsync(source, Ct);

        Assert.Equal((true, false, true), (first, second, nextDay));
    }

    /// <summary>没设上限时不计数；Redis 不可用时放行（可用性优先，与频率限制相同）</summary>
    [Fact]
    public async Task TryConsume_AllowsWithoutLimitOrRedis()
    {
        var unavailable = new FakeRedisCache { Unavailable = true };
        var time = new FixedTimeProvider(Today);

        var noLimit = await Create(unavailable, new RecordingAlertService(), time).TryConsumeAsync(Source(dailyLimit: null), Ct);
        var redisDown = await Create(unavailable, new RecordingAlertService(), time).TryConsumeAsync(Source(dailyLimit: 1), Ct);

        Assert.Equal((true, true), (noLimit, redisDown));
    }

    private static DataSourceDailyQuota Create(FakeRedisCache cache, RecordingAlertService alerts, TimeProvider time)
        => new(cache, alerts, time, NullLogger<DataSourceDailyQuota>.Instance);

    private static DataSource Source(int? dailyLimit) => new() { Id = 7, Name = "BLS API", DailyLimit = dailyLimit };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
