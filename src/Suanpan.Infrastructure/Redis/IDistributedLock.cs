namespace Suanpan.Infrastructure.Redis;

/// <summary>
/// 分布式锁（Redis SET NX PX 加锁，Lua 脚本比对持有者后释放），用于防止多个实例同时执行同一段逻辑：
/// <code>
/// await using var handle = await distributedLock.AcquireAsync($"collect:{sourceId}", TimeSpan.FromMinutes(2));
/// if (handle is null) return; // 别的实例正在执行，跳过本轮
/// </code>
/// 过期时间必须大于临界区的最长耗时，否则锁过期后别人会进来；释放时比对持有者，不会删掉别人的锁。
/// </summary>
public interface IDistributedLock
{
    /// <param name="key">锁名，实际键为 {InstanceName}lock:{key}</param>
    /// <param name="expiry">锁自动过期的时间，防止持有者崩溃后锁永远不释放</param>
    /// <param name="wait">最长等待时间；为空时不等待，拿不到立即返回 null</param>
    /// <returns>锁句柄（释放句柄即释放锁）；拿不到锁时返回 null</returns>
    Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, TimeSpan? wait = null, CancellationToken ct = default);
}
