namespace Suanpan.Infrastructure.Redis;

/// <summary>Redis 配置（配置节 Redis，docs/conventions/backend.md §12）</summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis 连接串（含密码），只从 user-secrets 或环境变量读取</summary>
    public string Configuration { get; set; } = string.Empty;

    /// <summary>键前缀；与其他项目共用 Redis 时靠它隔离</summary>
    public string InstanceName { get; set; } = "suanpan:";
}
