using Suanpan.Infrastructure.Redis;

namespace Suanpan.Services.Tests.Fixtures;

/// <summary>分布式锁的替身：Held 为 true 时模拟锁被别的实例持有；Redis 锁本身在 Infrastructure.Tests 中测试</summary>
internal sealed class StubDistributedLock : IDistributedLock
{
    public bool Held { get; set; }

    public Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, TimeSpan? wait = null, CancellationToken ct = default)
        => Task.FromResult(Held ? null : (IAsyncDisposable?)new Handle());

    private sealed class Handle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
