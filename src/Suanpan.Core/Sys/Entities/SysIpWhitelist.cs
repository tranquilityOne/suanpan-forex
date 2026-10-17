using Suanpan.Core.Common.Auditing;

namespace Suanpan.Core.Sys.Entities;

/// <summary>后台访问 IP 白名单（sys_ip_whitelist），总开关为配置 security:admin_ip_whitelist_enabled。</summary>
public class SysIpWhitelist : AuditedEntity
{
    public long Id { get; set; }

    /// <summary>单个 IP 或 CIDR 网段（IPv4、IPv6），唯一</summary>
    public string IpValue { get; set; } = string.Empty;

    /// <summary>备注，如"办公室网络"</summary>
    public string? Remark { get; set; }

    /// <summary>停用的条目不参与匹配</summary>
    public bool IsEnabled { get; set; } = true;
}
