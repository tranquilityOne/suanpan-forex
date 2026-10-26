using System.Collections.Concurrent;
using System.Net;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Runs.Models;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Collect.Sources.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Reference.Indicators.Models;
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

    /// <summary>管理员：快照中没有具体权限码，靠 IsSuperAdmin 放行</summary>
    public void GrantSuperAdmin(long userId)
        => _snapshots[userId] = new PermissionSnapshot(true, ["super_admin"], new HashSet<string>(StringComparer.Ordinal));

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

/// <summary>记录写入的操作日志和导出时收到的查询条件；导出写出一行固定内容</summary>
public sealed class RecordingOpLogService : IOpLogService
{
    public const string ExportContent = "exported";

    public List<SysOpLog> Written { get; } = [];

    public List<OpLogQuery> ExportQueries { get; } = [];

    public Task AddAsync(SysOpLog log, CancellationToken ct = default)
    {
        lock (Written)
        {
            Written.Add(log);
        }

        return Task.CompletedTask;
    }

    public Task<PagedResult<OpLogRow>> QueryAsync(OpLogQuery query, CancellationToken ct = default) => throw new NotSupportedException();

    public async Task<int> ExportAsync(OpLogQuery query, Stream output, TimeZoneInfo timeZone, CancellationToken ct = default)
    {
        ExportQueries.Add(query);
        await output.WriteAsync(System.Text.Encoding.UTF8.GetBytes(ExportContent), ct);
        return 1;
    }
}

/// <summary>按编码返回预设的指标；修改时记下输入并返回成功</summary>
public sealed class FakeIndicatorService : IIndicatorService
{
    public Dictionary<string, Indicator> Indicators { get; } = [];

    public List<IndicatorInput> Updated { get; } = [];

    public Task<Indicator?> GetAsync(string code, CancellationToken ct = default) => Task.FromResult(Indicators.GetValueOrDefault(code));

    public Task<ManageResult> UpdateAsync(IndicatorInput input, CancellationToken ct = default)
    {
        Updated.Add(input);
        return Task.FromResult(ManageResult.Ok);
    }

    public Task<PagedResult<IndicatorRow>> QueryAsync(IndicatorQuery query, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<ManageResult> CreateAsync(IndicatorInput input, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<ManageResult> SetEnabledAsync(string code, bool enabled, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<ManageResult> DeleteAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>新增时记下输入并返回成功</summary>
public sealed class FakeDataSourceService : IDataSourceService
{
    public List<DataSourceInput> Created { get; } = [];

    public Task<ManageResult> CreateAsync(DataSourceInput input, CancellationToken ct = default)
    {
        Created.Add(input);
        return Task.FromResult(ManageResult.Ok);
    }

    public Task<IReadOnlyList<DataSourceRow>> GetListAsync(CancellationToken ct = default) => throw new NotSupportedException();

    public Task<DataSource?> GetAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<DataSource>> GetOptionsAsync(CancellationToken ct = default) => throw new NotSupportedException();

    public Task<ManageResult> UpdateAsync(long id, DataSourceInput input, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<ManageResult> SetEnabledAsync(long id, bool enabled, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<DataSourceTestResult> TestAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>立即执行时记下参数并返回预设的结果；运行详情按 id 返回预设的记录</summary>
public sealed class FakeCollectRunService : ICollectRunService
{
    public List<(long TaskId, string TriggeredBy, bool AnyTargetType)> Triggered { get; } = [];

    public EnqueueResult TriggerResult { get; set; } = new(true, null, RunId: 42, Published: true);

    public Dictionary<long, CollectRunDetail> Runs { get; } = [];

    public Task<EnqueueResult> TriggerAsync(long taskId, string triggeredBy, bool anyTargetType, CancellationToken ct = default)
    {
        Triggered.Add((taskId, triggeredBy, anyTargetType));
        return Task.FromResult(TriggerResult);
    }

    public Task<CollectRunDetail?> GetAsync(long id, CancellationToken ct = default) => Task.FromResult(Runs.GetValueOrDefault(id));

    public Task<PagedResult<CollectRunRow>> QueryAsync(CollectRunQuery query, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<EnqueueResult> EnqueueAsync(
        long taskId, CollectRunTrigger trigger, DateTime? scheduledFor, string? triggeredBy, CancellationToken ct = default)
        => throw new NotSupportedException();
}
