namespace Suanpan.Infrastructure.Redis;

/// <summary>
/// Redis 常用操作（值用 JSON 序列化，键自动加 InstanceName 前缀）。
/// 跨进程共享的缓存、计数器、幂等标记用这里；只在本进程内用的缓存用 IMemoryCache。
/// 键用常量前缀，必须设置明确的过期时间，由写数据的一方负责让缓存失效（docs/conventions/backend.md §8.1）。
/// </summary>
public interface IRedisCache
{
    Task<T?> GetAsync<T>(string key);

    Task SetAsync<T>(string key, T value, TimeSpan expiry);

    /// <summary>键不存在时才写入（SET NX），返回是否写入成功；用作幂等标记、去重</summary>
    Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan expiry);

    Task<bool> RemoveAsync(string key);

    Task<bool> ExistsAsync(string key);

    /// <summary>原子加 1 并返回新值；键是新建的（返回 1）时设置过期时间</summary>
    Task<long> IncrementAsync(string key, TimeSpan expiryOnCreate);
}
