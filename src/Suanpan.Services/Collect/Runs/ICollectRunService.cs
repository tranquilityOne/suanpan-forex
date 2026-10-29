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
    /// 发布开始执行的指令：重新解析发 ReparseCommand，其他发第 1 次请求的 CollectRunCommand。创建运行记录后调用；
    /// 调度分发重新下发排队过久的记录时也调用（采集设计 §4.2）。失败时记警告日志并返回 false，不抛出
    /// </summary>
    Task<bool> TryPublishAsync(long runId, CollectRunTrigger trigger, CancellationToken ct = default);

    /// <summary>重新解析会处理的快照数（FR-004 主流程第 3 步），发起前给操作者确认</summary>
    Task<int> CountForReparseAsync(ReparseSelection selection, CancellationToken ct = default);

    /// <summary>
    /// 发起重新解析（采集设计 §5.6）：按 <see cref="Core.Collect.Rules.ReparseRule"/> 校验解析器和快照数（BR-024，ERR-PARSE-004），
    /// 创建"排队"的运行记录（触发方式"重新解析"，记下解析器和快照 id），提交后发布 ReparseCommand。
    /// 不发请求，所以不要求任务和数据源启用
    /// </summary>
    Task<EnqueueResult> ReparseAsync(ReparseSelection selection, string parserKey, string triggeredBy, CancellationToken ct = default);
}
