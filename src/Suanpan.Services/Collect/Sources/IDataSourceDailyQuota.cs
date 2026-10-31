using Suanpan.Core.Collect.Entities;

namespace Suanpan.Services.Collect.Sources;

/// <summary>
/// 数据源的每日请求上限（BR-003，采集设计 §4.3）：按 UTC 自然日计数，采集、盯盘、连通性测试共用。
/// 用到 90% 时告警一次；用完后当日不再请求（ERR-COL-010），第一次被拦下时再告警一次
/// </summary>
public interface IDataSourceDailyQuota
{
    /// <summary>
    /// 发请求前调用，计入当日的请求数：没设每日上限时直接放行；超过上限时返回 false，调用方不发请求。
    /// Redis 不可用时放行并记 Error 日志（与频率限制相同，可用性优先）
    /// </summary>
    Task<bool> TryConsumeAsync(DataSource source, CancellationToken ct = default);
}
