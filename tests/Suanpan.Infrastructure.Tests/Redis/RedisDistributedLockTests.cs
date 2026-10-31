using Microsoft.Extensions.Options;
using Suanpan.Infrastructure.Redis;
using Suanpan.Infrastructure.Tests.Fixtures;
using Xunit;

namespace Suanpan.Infrastructure.Tests.Redis;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class RedisDistributedLockTests(ValkeyFixture valkey)
{
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(1);

    private readonly IOptions<RedisOptions> _options = ValkeyFixture.UniqueOptions();

    [Fact]
    public async Task SecondAcquire_FailsUntilFirstIsReleased()
    {
        var distributedLock = await CreateLockAsync();

        var first = await distributedLock.AcquireAsync("job", Expiry, ct: Ct);
        Assert.NotNull(first);
        Assert.Null(await distributedLock.AcquireAsync("job", Expiry, ct: Ct));
        Assert.NotNull(await distributedLock.AcquireAsync("other", Expiry, ct: Ct));

        await first.DisposeAsync();
        Assert.NotNull(await distributedLock.AcquireAsync("job", Expiry, ct: Ct));
    }

    [Fact]
    public async Task AcquireWithWait_GetsLockOnceHolderReleases()
    {
        var distributedLock = await CreateLockAsync();
        var holder = await distributedLock.AcquireAsync("job", Expiry, ct: Ct);

        var waiting = distributedLock.AcquireAsync("job", Expiry, wait: TimeSpan.FromSeconds(10), ct: Ct);
        await holder!.DisposeAsync();

        Assert.NotNull(await waiting);
    }

    /// <summary>锁过期后被别人拿到，原持有者释放时不能删掉别人的锁</summary>
    [Fact]
    public async Task Release_KeepsLockTakenOverByAnotherHolder()
    {
        var handle = await (await CreateLockAsync()).AcquireAsync("job", Expiry, ct: Ct);
        var key = _options.Value.InstanceName + "lock:job";
        var db = (await valkey.GetRedisAsync()).GetDatabase();
        await db.StringSetAsync(key, "another-holder");

        await handle!.DisposeAsync();

        Assert.Equal("another-holder", await db.StringGetAsync(key));
    }

    /// <summary>续期（盯盘租约，采集设计 §5.7）：自己持有时重设过期时间；被别人拿走或已释放后续期失败</summary>
    [Fact]
    public async Task Extend_OnlyWhileStillHeld()
    {
        var handle = await (await CreateLockAsync()).AcquireAsync("lease", TimeSpan.FromSeconds(5), ct: Ct);
        var key = _options.Value.InstanceName + "lock:lease";
        var db = (await valkey.GetRedisAsync()).GetDatabase();

        var extended = await handle!.ExtendAsync(TimeSpan.FromMinutes(10));
        var ttl = await db.KeyTimeToLiveAsync(key);
        await db.StringSetAsync(key, "another-holder");
        var takenOver = await handle.ExtendAsync(TimeSpan.FromMinutes(10));

        Assert.True(extended);
        Assert.True(ttl > TimeSpan.FromMinutes(9));
        Assert.False(takenOver);
    }

    private async Task<RedisDistributedLock> CreateLockAsync() => new(await valkey.GetRedisAsync(), _options, TimeProvider.System);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
