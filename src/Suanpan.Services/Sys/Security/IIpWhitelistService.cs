using System.Net;
using Suanpan.Core.Sys.Entities;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Security;

/// <summary>
/// 后台访问 IP 白名单的维护和判断（sys 设计 §5.5）。判断走 30 秒内存缓存，本实例增删改后立即生效。
/// 总开关和应急放行 IP 由 Crm 的中间件处理。
/// </summary>
public interface IIpWhitelistService
{
    Task<IReadOnlyList<SysIpWhitelist>> GetListAsync(CancellationToken ct = default);

    Task<SysIpWhitelist?> GetAsync(long id, CancellationToken ct = default);

    /// <summary>新增或修改（Id 大于 0 为修改）；保存前校验格式和重复</summary>
    Task<ManageResult> SaveAsync(SysIpWhitelist item, CancellationToken ct = default);

    Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>客户端 IP 是否命中任一启用的条目（调用方先用 IpWhitelistMatcher.Normalize 处理）</summary>
    Task<bool> IsAllowedAsync(IPAddress clientIp, CancellationToken ct = default);
}
