using Suanpan.Services.Sys.Configs.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Configs;

/// <summary>
/// 系统配置（sys 设计 §10.2，后端约定 §7、§12）：业务动态开关，读取走 60 秒本机内存缓存，保存后本实例立即生效。
/// 键不存在或值无法解析时返回调用方传入的默认值。基础设施配置不放这里。
/// </summary>
public interface IConfigService
{
    Task<string?> GetStringAsync(string key, string? defaultValue, CancellationToken ct = default);

    Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default);

    Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken ct = default);

    Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default);

    /// <summary>页面用：代码中定义的配置项合并库中的值，按分组和键排序</summary>
    Task<IReadOnlyList<ConfigRow>> GetListAsync(CancellationToken ct = default);

    /// <summary>保存配置值：按类型校验并规范化，库里没有就新建；只能保存代码中定义过、或库里已有的键</summary>
    Task<ManageResult> SaveAsync(string key, string value, CancellationToken ct = default);

    /// <summary>恢复默认值：删除库中的值</summary>
    Task<ManageResult> ResetAsync(string key, CancellationToken ct = default);
}
