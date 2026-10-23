using Suanpan.Core.Collect.Entities;
using Suanpan.Services.Collect.Sources.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Collect.Sources;

/// <summary>数据源（采集设计 §3.1、§7.2，FR-001）</summary>
public interface IDataSourceService
{
    /// <summary>全部数据源，按名称排序</summary>
    Task<IReadOnlyList<DataSourceRow>> GetListAsync(CancellationToken ct = default);

    /// <summary>凭据为密文，调用方不能显示或输出</summary>
    Task<DataSource?> GetAsync(long id, CancellationToken ct = default);

    /// <summary>下拉框选项：只有启用的数据源</summary>
    Task<IReadOnlyList<DataSource>> GetOptionsAsync(CancellationToken ct = default);

    /// <summary>
    /// 新建为停用状态（FR-001 验收第 1 条）。失败时 Field 指向出错的字段：名称重复、经济体不存在或已停用、
    /// 字段规则（DataSourceRule，含 ERR-SRC-004）
    /// </summary>
    Task<ManageResult> CreateAsync(DataSourceInput input, CancellationToken ct = default);

    /// <summary>凭据输入为空时保留原值；换成别的经济体时新经济体必须启用</summary>
    Task<ManageResult> UpdateAsync(long id, DataSourceInput input, CancellationToken ct = default);

    /// <summary>启用时校验合规信息（BR-002、ERR-SRC-001）</summary>
    Task<ManageResult> SetEnabledAsync(long id, bool enabled, CancellationToken ct = default);

    /// <summary>被引用时拒绝，提示中列出引用方（BR-006、ERR-SRC-003）</summary>
    Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 连通性测试（PRD FR-001 主流程第 2 步）：GET 基础地址，超时 10 秒，受频率上限约束；robots.txt 为"不允许"时不发请求。
    /// 失败不影响保存（ERR-SRC-002）
    /// </summary>
    Task<DataSourceTestResult> TestAsync(long id, CancellationToken ct = default);
}
