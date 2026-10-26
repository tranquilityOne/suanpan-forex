using Suanpan.Core.Collect.Entities;
using Suanpan.Services.Collect.Tasks.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Collect.Tasks;

/// <summary>采集任务（采集设计 §3.2、§7.2，FR-002）。手动触发见 ICollectRunService</summary>
public interface ICollectTaskService
{
    /// <summary>按名称排序，带最近一次运行的状态和下次执行时间</summary>
    Task<PagedResult<CollectTaskRow>> QueryAsync(CollectTaskQuery query, CancellationToken ct = default);

    Task<CollectTask?> GetAsync(long id, CancellationToken ct = default);

    /// <summary>全部任务（含停用的），按名称排序；运行记录的筛选下拉框使用</summary>
    Task<IReadOnlyList<CollectTask>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 新建为停用状态。失败时 Field 指向出错的字段：名称重复、数据源不存在、解析器不在目录中、cron 写法错误（ERR-COL-001）、
    /// 参数不是 JSON 对象
    /// </summary>
    Task<ManageResult> CreateAsync(CollectTaskInput input, CancellationToken ct = default);

    /// <summary>修改 cron 时重置调度的记账时间，新规则从下一个时间点开始生效（FR-002 验收第 1 条）</summary>
    Task<ManageResult> UpdateAsync(long id, CollectTaskInput input, CancellationToken ct = default);

    /// <summary>启用时所属数据源必须是启用状态（PRD FR-002 前置条件）；启用时重置调度的记账时间</summary>
    Task<ManageResult> SetEnabledAsync(long id, bool enabled, CancellationToken ct = default);

    /// <summary>有运行记录的任务不能删除，只能停用（BR-154）</summary>
    Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>接下来 <see cref="CollectTaskService.PreviewCount"/> 次执行时间，表单上用来核对 UTC 换算</summary>
    CronPreview PreviewSchedule(string? cron);
}
