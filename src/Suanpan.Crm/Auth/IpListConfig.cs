using Suanpan.Services.Sys.Security;

namespace Suanpan.Crm.Auth;

/// <summary>读取逗号分隔的 IP 或 CIDR 网段配置；格式与 IP 白名单条目相同，有错误时启动即报错</summary>
internal static class IpListConfig
{
    public static IReadOnlyList<string> Parse(IConfiguration configuration, string key)
    {
        var entries = (configuration[key] ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var entry in entries)
        {
            if (IpWhitelistMatcher.Validate(entry) is { } error)
            {
                throw new InvalidOperationException($"配置 {key} 中的 {entry} 无效：{error}");
            }
        }

        return entries;
    }
}
