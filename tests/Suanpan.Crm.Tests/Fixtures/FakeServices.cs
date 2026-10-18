using System.Collections.Concurrent;
using System.Net;
using Suanpan.Core.Sys.Entities;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Configs.Models;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Logging.Models;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Models;
using Suanpan.Services.Sys.Security;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Tests.Fixtures;

// 管道测试只验证 Crm 的接线；这些服务本身连真实数据库的行为在 Suanpan.Services.Tests 中测试。

/// <summary>只支持 IP 白名单总开关</summary>
public sealed class FakeConfigService : IConfigService
{
    public bool IpWhitelistEnabled { get; set; }

    public Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default)
        => Task.FromResult(key == SysConfigKeys.AdminIpWhitelistEnabled ? IpWhitelistEnabled : defaultValue);

    public Task<string?> GetStringAsync(string key, string? defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

    public Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

    public Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

    public Task<IReadOnlyList<ConfigRow>> GetListAsync(CancellationToken ct = default) => throw new NotSupportedException();

    public Task<ManageResult> SaveAsync(string key, string value, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<ManageResult> ResetAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
}

public sealed class FakeIpWhitelistService : IIpWhitelistService
{
    public HashSet<IPAddress> Allowed { get; } = [];

    public Task<bool> IsAllowedAsync(IPAddress clientIp, CancellationToken ct = default) => Task.FromResult(Allowed.Contains(clientIp));

    public Task<IReadOnlyList<SysIpWhitelist>> GetListAsync(CancellationToken ct = default) => throw new NotSupportedException();

    public Task<SysIpWhitelist?> GetAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<ManageResult> SaveAsync(SysIpWhitelist item, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>按账号 id 返回预设的权限码；没有预设的账号返回空快照</summary>
public sealed class FakePermissionService : IPermissionService
{
    private readonly Dictionary<long, PermissionSnapshot> _snapshots = [];

    public void Grant(long userId, params string[] permCodes)
        => _snapshots[userId] = new PermissionSnapshot(false, ["tester"], new HashSet<string>(permCodes, StringComparer.Ordinal));

    public Task<PermissionSnapshot> GetSnapshotAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(_snapshots.GetValueOrDefault(userId, PermissionSnapshot.Empty));

    public void Invalidate(long userId)
    {
    }

    public Task InvalidateRoleHoldersAsync(long roleId, CancellationToken ct = default) => Task.CompletedTask;

    public void InvalidateAll()
    {
    }
}

/// <summary>按账号 id 返回预设的菜单树</summary>
public sealed class FakeMenuService : IMenuService
{
    private readonly Dictionary<long, IReadOnlyList<MenuNode>> _menus = [];

    public void Set(long userId, params MenuNode[] directories) => _menus[userId] = directories;

    public Task<IReadOnlyList<MenuNode>> GetUserMenusAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(_menus.GetValueOrDefault(userId, []));
}

/// <summary>按账号记录当前安全戳；没有记录的账号，当前安全戳为 <see cref="ValidStamp"/></summary>
public sealed class FakeSecurityStampValidator : ISecurityStampValidator
{
    public const string ValidStamp = "current-stamp";

    private readonly ConcurrentDictionary<long, string> _stamps = new();

    public string Current(long userId) => _stamps.GetValueOrDefault(userId, ValidStamp);

    /// <summary>刷新安全戳（改密码、绑定 TOTP 时），返回新值</summary>
    public string Renew(long userId) => _stamps[userId] = Guid.NewGuid().ToString("N");

    public Task<bool> ValidateAsync(long userId, string? stamp, CancellationToken ct = default) => Task.FromResult(stamp == Current(userId));

    public void Invalidate(long userId)
    {
    }
}

public sealed class RecordingOpLogService : IOpLogService
{
    public List<SysOpLog> Written { get; } = [];

    public Task AddAsync(SysOpLog log, CancellationToken ct = default)
    {
        lock (Written)
        {
            Written.Add(log);
        }

        return Task.CompletedTask;
    }

    public Task<PagedResult<OpLogRow>> QueryAsync(OpLogQuery query, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<int> ExportAsync(OpLogQuery query, Stream output, TimeZoneInfo timeZone, CancellationToken ct = default)
        => throw new NotSupportedException();
}
