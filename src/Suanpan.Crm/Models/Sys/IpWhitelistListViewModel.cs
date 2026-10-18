using Suanpan.Core.Sys.Entities;

namespace Suanpan.Crm.Models.Sys;

/// <summary>IP 白名单列表，附带总开关状态和当前访问者的 IP，方便判断改动会不会把自己挡在外面</summary>
public sealed record IpWhitelistListViewModel(
    IReadOnlyList<SysIpWhitelist> Items,
    bool SwitchEnabled,
    string? CurrentIp);
