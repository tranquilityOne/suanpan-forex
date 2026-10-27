using Suanpan.Core.Collect.Enums;
using Suanpan.Services.Collect.Runs.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Collect.Runs;

/// <summary>运行记录（采集设计 §4、§7.2，FR-002）：查询，以及创建运行记录并下发给 Collector</summary>
public interface ICollectRunService
{
    /// <summary>按触发时间倒序，可按任务、数据源、状态、触发方式、时间筛选（FR-002 验收第 5 条）</summary>
    Task<PagedResult<CollectRunRow>> QueryAsync(CollectRunQuery query, CancellationToken ct = default);

    Task<CollectRunDetail?> GetAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 创建"排队"的运行记录，提交后发布 CollectRunCommand（采集设计 §4.1），Job 的调度分发和 Crm 的手动触发共用。
    /// 所属数据源必须启用；停用的任务只能手动触发（BR-155）
    /// </summary>
    Task<EnqueueResult> EnqueueAsync(
        long taskId, CollectRunTrigger trigger, DateTime? scheduledFor, string? triggeredBy, CancellationToken ct = default);

    /// <summary>
    /// 后台的"立即执行"（采集设计 §4.4、§8.2）：触发方式为手动，记录触发人。
    /// anyTargetType 为 false 时只能触发日历排期、公布值类任务（编辑的权限，PRD §13.1.2）
    /// </summary>
    Task<EnqueueResult> TriggerAsync(long taskId, string triggeredBy, bool anyTargetType, CancellationToken ct = default);

    /// <summary>
    /// 发布第 1 次请求的指令。EnqueueAsync 创建后调用；调度分发重新下发排队过久的记录时也调用（采集设计 §4.2）。
    /// 失败时记警告日志并返回 false，不抛出
    /// </summary>
    Task<bool> TryPublishAsync(long runId, CancellationToken ct = default);
}
