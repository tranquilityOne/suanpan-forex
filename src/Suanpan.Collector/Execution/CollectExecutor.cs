using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Ops.Enums;
using Suanpan.Data;
using Suanpan.Infrastructure.Http;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Collect.Ingest;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Collect.Snapshots.Models;
using Suanpan.Services.Common.Alerts;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Security;
using Suanpan.Shared.Messages.Collect;

namespace Suanpan.Collector.Execution;

/// <summary>
/// 执行一条运行记录的一次请求（采集设计 §4.3）。每条 CollectRunCommand 对应一次请求：
/// <list type="number">
/// <item>幂等：第一次请求时运行记录必须是"排队"，第 n 次（重试）时必须是"运行中"且已请求 n-1 次，否则是重复投递，直接忽略</item>
/// <item>前置检查：数据源必须启用；任务也必须启用，手动触发的除外（BR-155）。不满足时记为"跳过"，不发请求</item>
/// <item>认领：第一次请求在任务锁内检查同一任务有没有其他"运行中"的记录（BR-010），有则跳过（ERR-COL-004）；
/// 认领时写入本次的请求记录（开始时间），执行中断的回收据此判断</item>
/// <item>请求 → 保存快照（FR-004，先存快照再解析）→ 解析 → 入库；可重试的失败写入 next_attempt_at 并延时下发下一次请求（BR-009），用完记为失败</item>
/// </list>
/// 改运行记录都在行锁内进行，与重复投递的指令、执行中断的回收互斥
/// </summary>
public sealed class CollectExecutor(
    SuanpanDbContext db,
    IDistributedLock distributedLock,
    IRateLimiter rateLimiter,
    IHttpClientFactory httpClientFactory,
    ISecretProtector secretProtector,
    IConfigService config,
    IEventBus eventBus,
    IEnumerable<ICollectAdapter> adapters,
    IEnumerable<IParser> parsers,
    IEnumerable<ICollectIngestor> ingestors,
    ISnapshotService snapshots,
    IAlertService alerts,
    TimeProvider timeProvider,
    ILogger<CollectExecutor> logger)
{
    /// <summary>任务锁只保护"检查并发 + 改状态"这一小段，不在整个执行期间持有</summary>
    private static readonly TimeSpan TaskLockExpiry = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan TaskLockWait = TimeSpan.FromSeconds(10);

    private const int MaxErrorMessageLength = 2000;

    /// <summary>运行记录的 parse_output 最多存这么多条记录（采集设计 §2.1）</summary>
    private const int MaxParseOutputRecords = 1000;

    private static readonly JsonSerializerOptions ParseOutputJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ExecuteAsync(CollectRunCommand command, CancellationToken ct)
    {
        var run = await db.CollectRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == command.RunId, ct);
        if (run is null || !IsExpected(run, command.Attempt))
        {
            logger.LogInformation("运行记录 {RunId} 的第 {Attempt} 次请求已经处理过或记录不存在，忽略这条指令", command.RunId, command.Attempt);
            return;
        }

        var task = await db.CollectTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == run.TaskId, ct);
        var source = await db.DataSources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == run.SourceId, ct);
        if (SkipReason(run, task, source) is { } reason)
        {
            // 每次请求前都检查，停用后不再发起新请求（FR-001 验收第 4 条）
            await UpdateRunAsync(run.Id, r => IsExpected(r, command.Attempt), r => End(r, CollectRunStatus.Skipped, null, reason), ct);
            return;
        }

        var parser = parsers.FirstOrDefault(p => p.Key == task!.ParserKey);
        var adapterKey = task!.ParserKey[..Math.Max(task.ParserKey.LastIndexOf('.'), 0)];
        var adapter = adapters.FirstOrDefault(a => a.Key == adapterKey);
        var ingestor = ingestors.FirstOrDefault(i => i.TargetType == task.TargetType);
        if (parser is null || adapter is null || ingestor is null)
        {
            var missing = parser is null ? $"没有解析器 {task.ParserKey} 的实现"
                : adapter is null ? $"没有适配器 {adapterKey} 的实现"
                : $"没有{CollectDisplayNames.Get(task.TargetType)}的入库服务";
            logger.LogError("运行记录 {RunId}（任务 {TaskId}）{Missing}，记为失败", run.Id, task.Id, missing);
            await UpdateRunAsync(run.Id, r => IsExpected(r, command.Attempt), r => End(r, CollectRunStatus.Failed, null, missing), ct);
            return;
        }

        var startedAt = Now;
        var claimed = command.Attempt == 1
            ? await ClaimFirstAttemptAsync(run, startedAt, ct)
            : await UpdateRunAsync(run.Id, r => IsExpected(r, command.Attempt), r => BeginAttempt(r, command.Attempt, startedAt), ct);
        if (claimed)
        {
            await RunAttemptAsync(run.Id, command.Attempt, task, source!, adapter, parser, ingestor, ct);
        }
    }

    /// <summary>第 1 次请求的指令要求"排队"；第 n 次要求"运行中"且已请求 n-1 次。其余都是重复投递或已经结束</summary>
    private static bool IsExpected(CollectRun run, int attempt)
        => attempt == 1
            ? run.Status == CollectRunStatus.Queued
            : attempt > 1 && run.Status == CollectRunStatus.Running && run.AttemptCount == attempt - 1;

    private static string? SkipReason(CollectRun run, CollectTask? task, DataSource? source)
    {
        if (task is null)
        {
            return "采集任务不存在";
        }

        if (source is null)
        {
            return "数据源不存在";
        }

        if (!source.IsEnabled)
        {
            return $"数据源 {source.Name} 已停用";
        }

        return !task.IsEnabled && run.Trigger != CollectRunTrigger.Manual ? $"采集任务 {task.Name} 已停用" : null;
    }

    /// <summary>在任务锁内检查同一任务有没有其他"运行中"的记录（含等待重试的），有则跳过（BR-010，ERR-COL-004）</summary>
    private async Task<bool> ClaimFirstAttemptAsync(CollectRun run, DateTime startedAt, CancellationToken ct)
    {
        await using var handle = await distributedLock.AcquireAsync($"collect:task:{run.TaskId}", TaskLockExpiry, TaskLockWait, ct)
            ?? throw new InvalidOperationException($"采集任务 {run.TaskId} 的锁一直被占用，稍后重试这条指令");

        var busy = await db.CollectRuns.AsNoTracking()
            .AnyAsync(r => r.TaskId == run.TaskId && r.Id != run.Id && r.Status == CollectRunStatus.Running, ct);
        if (busy)
        {
            await UpdateRunAsync(run.Id, r => r.Status == CollectRunStatus.Queued,
                r => End(r, CollectRunStatus.Skipped, CollectErrorCodes.ConcurrentRun, "同一任务上一次运行还没有结束"), ct);
            return false;
        }

        return await UpdateRunAsync(run.Id, r => r.Status == CollectRunStatus.Queued, r =>
        {
            r.Status = CollectRunStatus.Running;
            r.StartedAt = startedAt;
            BeginAttempt(r, 1, startedAt);
        }, ct);
    }

    private async Task RunAttemptAsync(
        long runId, int attempt, CollectTask task, DataSource source, ICollectAdapter adapter, IParser parser, ICollectIngestor ingestor,
        CancellationToken ct)
    {
        var credential = source.Credential is { } encrypted ? secretProtector.Decrypt(encrypted) : null;
        var context = new FetchContext(source, credential, task.Params, httpClientFactory.CreateClient(CollectHttp.ClientName), rateLimiter, timeProvider);

        // 4. 请求
        var started = timeProvider.GetTimestamp();
        FetchResult? fetched = null;
        int? httpStatus = null;
        string? error = null;
        var retryable = false;
        try
        {
            fetched = await adapter.FetchAsync(context, ct);
            var failed = fetched.Responses.FirstOrDefault(r => !r.IsSuccess);
            httpStatus = (failed ?? fetched.Responses.LastOrDefault())?.StatusCode;
            if (failed is not null)
            {
                error = $"HTTP {failed.StatusCode}：{PathOnly(failed.Url)}";
                retryable = RetryPolicy.IsRetryable(httpStatus);
            }
        }
        catch (FetchTimeoutException ex)
        {
            error = ex.Message;
            retryable = true;
        }
        catch (HttpRequestException ex)
        {
            httpStatus = (int?)ex.StatusCode;
            error = ex.Message;
            retryable = RetryPolicy.IsRetryable(httpStatus);
        }

        var durationMs = (int)timeProvider.GetElapsedTime(started).TotalMilliseconds;
        void Record(CollectAttempt a)
        {
            a.DurationMs = durationMs;
            a.HttpStatus = httpStatus;
            a.Error = error;
            a.DeferredMs = context.DeferredMs;
        }

        // 先存快照再判断成败、解析（FR-004，硬性规则 14）：非 2xx 的响应也存，排查被拦截、限流时能看到当时的内容
        IReadOnlyList<long> snapshotIds = [];
        if (fetched is not null)
        {
            try
            {
                snapshotIds = await SaveSnapshotsAsync(runId, attempt, task, fetched, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 快照写入失败（ERR-COL-007）：不解析、不重试，告警
                logger.LogError(ex, "运行记录 {RunId}（任务 {TaskId}）快照写入失败", runId, task.Id);
                await FinishAttemptAsync(runId, attempt, Record,
                    r => End(r, CollectRunStatus.Failed, CollectErrorCodes.SnapshotWriteFailed, $"快照写入失败：{ex.Message}"), ct);
                await RaiseAlertAsync(AlertLevel.Important, source.Name, $"运行记录 {runId}（任务 {task.Name}）快照写入失败：{ex.Message}", ct);
                return;
            }
        }

        if (error is not null)
        {
            var delays = await RetryDelaysAsync(ct);
            if (retryable && RetryPolicy.DelayAfter(attempt, delays) is { } delay)
            {
                var nextAttemptAt = Now + delay;
                if (await FinishAttemptAsync(runId, attempt, Record, r => r.NextAttemptAt = nextAttemptAt, ct))
                {
                    await PublishRetryAsync(runId, attempt + 1, delay, ct);
                }

                return;
            }

            logger.LogError("运行记录 {RunId}（任务 {TaskId}）请求失败，共请求 {Attempts} 次：{Error}", runId, task.Id, attempt, error);
            await FinishAttemptAsync(runId, attempt, Record,
                r => End(r, CollectRunStatus.Failed, CollectErrorCodes.RequestFailed, $"请求失败，共请求 {attempt} 次：{error}"), ct);
            return;
        }

        // 内容为空的响应：快照无效（ERR-PARSE-003），不解析
        var emptyIds = fetched!.Responses
            .Select((response, i) => (response, i))
            .Where(x => x.response.Body.Length == 0)
            .Select(x => snapshotIds[x.i])
            .ToList();
        if (emptyIds.Count > 0)
        {
            logger.LogError("运行记录 {RunId}（任务 {TaskId}）有 {Count} 个响应的内容为空", runId, task.Id, emptyIds.Count);
            await snapshots.MarkInvalidAsync(emptyIds, "内容为空", ct);
            await FinishAttemptAsync(runId, attempt, Record,
                r => End(r, CollectRunStatus.Failed, CollectErrorCodes.InvalidSnapshot, $"{emptyIds.Count} 个响应的内容为空"), ct);
            return;
        }

        // 5. 解析
        ParseResult parsed;
        try
        {
            parsed = parser.Parse(fetched, task.Params);
        }
        catch (InvalidSnapshotException ex)
        {
            // 内容是拦截页等：这次请求的快照都标记为无效，不参与重新解析（ERR-PARSE-003）
            logger.LogError("运行记录 {RunId}（任务 {TaskId}，解析器 {ParserKey}）快照无效：{Reason}", runId, task.Id, task.ParserKey, ex.Message);
            await snapshots.MarkInvalidAsync(snapshotIds, ex.Message, ct);
            await FinishAttemptAsync(runId, attempt, Record, r => End(r, CollectRunStatus.Failed, CollectErrorCodes.InvalidSnapshot, ex.Message), ct);
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "运行记录 {RunId}（任务 {TaskId}，解析器 {ParserKey}）解析失败", runId, task.Id, task.ParserKey);
            await FinishAttemptAsync(runId, attempt, Record, r => End(r, CollectRunStatus.Failed, CollectErrorCodes.ParseFailed, ex.Message), ct);
            return;
        }

        var parseOutput = ParseOutputJson(runId, parsed.Records);

        // 6. 入库：带上快照 id，入库服务据此记下来源快照（BR-045）
        IngestResult ingested;
        try
        {
            ingested = await ingestor.IngestAsync(new IngestRequest(runId, task.Id, source.Id, parsed.Records, snapshotIds), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "运行记录 {RunId}（任务 {TaskId}）入库失败", runId, task.Id);
            await FinishAttemptAsync(runId, attempt, Record, r =>
            {
                r.ParseOutput = parseOutput;
                End(r, CollectRunStatus.Failed, null, $"入库失败：{ex.Message}");
            }, ct);
            return;
        }

        // 7. 结束：部分记录缺失或入库失败时记为部分成功（ERR-PARSE-002）
        var problems = new List<string>();
        if (parsed.Missing.Count > 0)
        {
            problems.Add($"缺少 {parsed.Missing.Count} 项：{string.Join("、", parsed.Missing.Take(10))}");
        }

        if (ingested.Failed > 0)
        {
            problems.Add($"入库失败 {ingested.Failed} 条");
        }

        await FinishAttemptAsync(runId, attempt, Record, r =>
        {
            r.InsertedCount = ingested.Inserted;
            r.UpdatedCount = ingested.Updated;
            r.SkippedCount = ingested.Skipped;
            r.LockedCount = ingested.Locked;
            r.ParseOutput = parseOutput;
            if (problems.Count > 0)
            {
                End(r, CollectRunStatus.PartiallySucceeded, CollectErrorCodes.PartialData, string.Join("；", problems));
            }
            else
            {
                End(r, CollectRunStatus.Succeeded, null, null);
            }
        }, ct);
    }

    /// <summary>一次请求的全部响应存为快照（采集设计 §5.5），返回各响应的快照 id</summary>
    private async Task<IReadOnlyList<long>> SaveSnapshotsAsync(long runId, int attempt, CollectTask task, FetchResult fetched, CancellationToken ct)
    {
        var now = Now;
        var inputs = fetched.Responses
            .Select(r => new SnapshotInput(PathOnly(r.Url), r.RequestKey ?? PathOnly(r.Url), r.StatusCode, r.ContentType, r.Body, r.ReceivedAt ?? now))
            .ToList();
        var ids = await snapshots.SaveAsync(new SnapshotBatch(runId, (short)attempt, task.Id, task.SourceId, inputs), skipUnchanged: false, ct);
        return ids.Select(id => id ?? throw new InvalidOperationException("快照服务没有返回快照 id")).ToList();
    }

    /// <summary>
    /// 解析器输出的记录（最多前 1000 条）存进运行记录，快照详情中"当时的解析结果"用（FR-004 主流程第 2 步）；序列化失败时不存
    /// </summary>
    private string? ParseOutputJson(long runId, IReadOnlyList<object> records)
    {
        try
        {
            return JsonSerializer.Serialize(new { total = records.Count, records = records.Take(MaxParseOutputRecords) }, ParseOutputJsonOptions);
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "运行记录 {RunId} 的解析结果无法序列化，不保存", runId);
            return null;
        }
    }

    /// <summary>地址只保留路径：有的数据源把凭据放在查询串里</summary>
    private static string PathOnly(Uri url) => url.GetLeftPart(UriPartial.Path);

    /// <summary>告警是附带动作，失败只记日志，不影响执行（后端约定 §11）</summary>
    private async Task RaiseAlertAsync(AlertLevel level, string subject, string message, CancellationToken ct)
    {
        try
        {
            await alerts.RaiseAsync(level, subject, message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "告警发送失败：{Subject} {Message}", subject, message);
        }
    }

    /// <summary>认领第 n 次请求：请求次数加一，写入这次请求的记录（开始时间），清掉等待重试的时间</summary>
    private static void BeginAttempt(CollectRun run, int attempt, DateTime startedAt)
    {
        run.AttemptCount = (short)attempt;
        run.NextAttemptAt = null;
        run.Attempts.Add(new CollectAttempt { No = (short)attempt, StartedAt = startedAt });
    }

    /// <summary>补全第 n 次请求的记录并改运行记录；回收已经把它记为失败（执行中断）时不再改写</summary>
    private async Task<bool> FinishAttemptAsync(long runId, int attempt, Action<CollectAttempt> record, Action<CollectRun> change, CancellationToken ct)
    {
        var updated = await UpdateRunAsync(runId, r => r.Status == CollectRunStatus.Running && r.AttemptCount == attempt, r =>
        {
            if (r.Attempts.LastOrDefault(a => a.No == attempt) is { } current)
            {
                record(current);
            }

            change(r);
        }, ct);
        if (!updated)
        {
            logger.LogWarning("运行记录 {RunId} 的第 {Attempt} 次请求结束时，记录已不是运行中（可能已记为执行中断），本次结果没有写入", runId, attempt);
        }

        return updated;
    }

    private void End(CollectRun run, CollectRunStatus status, string? errorCode, string? errorMessage)
    {
        run.Status = status;
        run.EndedAt = Now;
        run.NextAttemptAt = null;
        run.ErrorCode = errorCode;
        run.ErrorMessage = errorMessage is { Length: > MaxErrorMessageLength } ? errorMessage[..MaxErrorMessageLength] : errorMessage;
    }

    /// <summary>
    /// 在行锁内读出运行记录，满足条件时修改并保存（后端约定 §6.4）：与重复投递的指令、执行中断的回收互斥。
    /// 状态只能按 CollectRunStatusRule 流转
    /// </summary>
    private async Task<bool> UpdateRunAsync(long runId, Func<CollectRun, bool> guard, Action<CollectRun> change, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM col_run WHERE id = {runId} FOR UPDATE", ct);
            var run = await db.CollectRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
            if (run is null || !guard(run))
            {
                return false;
            }

            var from = run.Status;
            change(run);
            if (run.Status != from && !CollectRunStatusRule.CanTransition(from, run.Status))
            {
                throw new InvalidOperationException($"运行记录 {runId} 的状态不能从 {from} 改为 {run.Status}");
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>延时下发下一次请求；发布失败时记录停在"运行中"，由执行中断的回收记为失败（ERR-COL-009）</summary>
    private async Task PublishRetryAsync(long runId, int attempt, TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await eventBus.PublishAsync(new CollectRunCommand(runId, attempt), delay, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "运行记录 {RunId} 的第 {Attempt} 次请求下发失败，将由执行中断的回收记为失败", runId, attempt);
        }
    }

    /// <summary>重试间隔取系统配置 collect:retry_delay_seconds（BR-009），格式不对时按默认值并记警告日志</summary>
    private async Task<IReadOnlyList<TimeSpan>> RetryDelaysAsync(CancellationToken ct)
    {
        var value = await config.GetStringAsync(CollectConfigKeys.RetryDelaySeconds, CollectConfigKeys.RetryDelaySecondsDefault, ct);
        if (RetryPolicy.ParseDelays(value) is { } delays)
        {
            return delays;
        }

        logger.LogWarning("系统配置 {Key} 的值 {Value} 格式不对，按默认值 {Default} 重试",
            CollectConfigKeys.RetryDelaySeconds, value, CollectConfigKeys.RetryDelaySecondsDefault);
        return RetryPolicy.ParseDelays(CollectConfigKeys.RetryDelaySecondsDefault)!;
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
}
