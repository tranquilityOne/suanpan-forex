using System.Net;
using System.Net.Sockets;

namespace Suanpan.Services.Sys.Security;

/// <summary>
/// IP 白名单条目的校验与匹配（纯函数）。条目是单个 IP 或 CIDR 网段，支持 IPv4、IPv6（sys 设计 §3.1）；
/// IPv4 与 IPv6 不互相匹配，IPv4 映射的 IPv6 地址（::ffff:1.2.3.4）由调用方先 <see cref="Normalize"/>。
/// </summary>
public static class IpWhitelistMatcher
{
    /// <summary>保存前校验格式，返回 null 表示合法，否则返回错误提示</summary>
    public static string? Validate(string? value)
    {
        value = value?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return "IP 不能为空";
        }

        if (value.Contains('/'))
        {
            return TryParseNetwork(value, out _)
                ? null
                : "CIDR 网段格式不正确（示例：203.0.113.0/24、2001:db8::/32），网段地址的主机位必须为 0";
        }

        return TryParseAddress(value, out _) ? null : "IP 格式不正确（示例：203.0.113.10、2001:db8::1）";
    }

    /// <summary>客户端 IP 是否命中条目；格式损坏的条目按不命中处理</summary>
    public static bool Matches(string entry, IPAddress client)
    {
        entry = entry.Trim();

        if (entry.Contains('/'))
        {
            return TryParseNetwork(entry, out var network)
                   && network.BaseAddress.AddressFamily == client.AddressFamily
                   && network.Contains(client);
        }

        return TryParseAddress(entry, out var exact) && exact.Equals(client);
    }

    /// <summary>把 IPv4 映射的 IPv6 地址还原为 IPv4，其余原样返回</summary>
    public static IPAddress Normalize(IPAddress ip)
        => ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    /// <summary>解析 CIDR 网段；网段地址的主机位必须为 0（203.0.113.10/24 这类写法容易被误解为只放行单个 IP）</summary>
    private static bool TryParseNetwork(string value, out IPNetwork network)
    {
        network = default;
        var slash = value.IndexOf('/');
        return TryParseAddress(value[..slash], out var address)
               && IPNetwork.TryParse(value, out network)
               && HostBitsAreZero(address, network.PrefixLength);
    }

    private static bool HostBitsAreZero(IPAddress address, int prefixLength)
    {
        var bytes = address.GetAddressBytes();
        for (var bit = prefixLength; bit < bytes.Length * 8; bit++)
        {
            if ((bytes[bit / 8] & (0x80 >> (bit % 8))) != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 比 <see cref="IPAddress.TryParse(string?, out IPAddress?)"/> 严格：IPv4 必须是完整的四段写法，
    /// 不接受 "10"、"10.1" 这类会被解释成其他地址的简写。
    /// </summary>
    private static bool TryParseAddress(string value, out IPAddress address)
    {
        address = IPAddress.None;
        if (!IPAddress.TryParse(value, out var parsed))
        {
            return false;
        }

        if (parsed.AddressFamily == AddressFamily.InterNetwork && value.Count(c => c == '.') != 3)
        {
            return false;
        }

        address = parsed;
        return true;
    }
}
