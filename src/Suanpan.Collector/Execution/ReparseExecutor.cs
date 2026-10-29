using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Data;
using Suanpan.Services.Collect.Ingest;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Shared.Messages.Collect;

namespace Suanpan.Collector.Execution;

/// <summary>
/// 执行一条重新解析的运行记录（采集设计 §5.6，FR-004 主流程第 3、4 步）：
/// <list type="number">
/// <item>认领：在行锁内把运行记录从"排队"改为"运行中"，不是"排队"的是重复投递，直接忽略。不参与同一任务的并发检查</item>
/// <item>选中的快照按"运行记录 + 第几次请求"分组，组内按序号排序还原成解析器的输入；一组一组地读内容、解析、入库，
/// 入库走与正常采集相同的入库服务（BR-023，锁定字段不覆盖）</item>
/// <item>无效的快照跳过并计数（ERR-PARSE-003）；某一组解析或入库失败时记下快照 id，继续下一组</item>
/// <item>每处理完一组累加条数，距上次心跳超过 30 秒时写心跳；回收按心跳判断执行中断（ERR-COL-009）</item>
/// <item>结束：全部成功为"成功"；有无效快照、失败的组或缺失项为"部分成功"；没有一组成功为"失败"</item>
/// </list>
/// </summary>
public sealed class ReparseExecutor(
    SuanpanDbContext db,
    IEnumerable<IParser> parsers,
    IEnumerable<ICollectIngestor> ingestors,
    ISnapshotService snapshots,
    ISnapshotContentStore contents,
    CollectRunWriter writer,
    ILogger<ReparseExecutor> logger)
{
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    /// <summary>结束原因中最多列出的快照 id 个数</summary>
    private const int MaxListedIds = 10;

    public async Task ExecuteAsync(ReparseCommand command, CancellationToken ct)
    {
        var run = await db.CollectRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == command.RunId, ct);
        if (run is not { Status: CollectRunStatus.Queued, Trigger: CollectRunTrigger.Reparse, Reparse: { } reparse })
        {
            logger.LogInformation("运行记录 {RunId} 不是排队中的重新解析，忽略这条指令（重复投递或记录不存在）", command.RunId);
            return;
        }

        var task = await db.CollectTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == run.TaskId, ct);
        var parser = parsers.FirstOrDefault(p => p.Key == reparse.ParserKey);
        var ingestor = task is null ? null : ingestors.FirstOrDefault(i => i.TargetType == task.TargetType);
        if (task is null || parser is null || ingestor is null)
        {
            var missing = task is null ? "采集任务不存在"
                : parser is null ? $"没有解析器 {reparse.ParserKey} 的实现"
                : $"没有{CollectDisplayNames.Get(task.TargetType)}的入库服务";
            logger.LogError("运行记录 {RunId}（重新解析）{Missing}，记为失败", run.Id, missing);
            await writer.UpdateAsync(run.Id, r => r.Status == CollectRunStatus.Queued, r => writer.End(r, CollectRunStatus.Failed, null, missing), ct);
            return;
        }

        var startedAt = writer.Now;
        var claimed = await writer.UpdateAsync(run.Id, r => r.Status == CollectRunStatus.Queued, r =>
        {
            r.Status = CollectRunStatus.Running;
            r.StartedAt = startedAt;
            r.HeartbeatAt = startedAt;
        }, ct);
        if (claimed)
        {
            await ReparseAsync(run.Id, task, parser, ingestor, reparse.SnapshotIds, startedAt, ct);
        }
    }

    private async Task ReparseAsync(
        long runId, CollectTask task, IParser parser, ICollectIngestor ingestor, IReadOnlyList<long> snapshotIds, DateTime startedAt, CancellationToken ct)
    {
        var groups = await LoadGroupsAsync(snapshotIds, ct);
        var progress = new Progress();
        var lastHeartbeat = startedAt;
        foreach (var group in groups)
        {
            await ProcessGroupAsync(runId, task, parser, ingestor, group, progress, ct);

            var now = writer.Now;
            var heartbeat = now - lastHeartbeat >= HeartbeatInterval;
            lastHeartbeat = heartbeat ? now : lastHeartbeat;
            var stillRunning = await writer.UpdateAsync(runId, r => r.Status == CollectRunStatus.Running, r =>
            {
                progress.WriteCounts(r);
                r.HeartbeatAt = heartbeat ? now : r.HeartbeatAt;
            }, ct);
            if (!stillRunning)
            {
                logger.LogWarning("运行记录 {RunId}（重新解析）已不是运行中（可能已记为执行中断），停止处理", runId);
                return;
            }
        }

        var (status, errorCode, message) = progress.Outcome(groups.Count == 0);
        var parseOutput = writer.ParseOutputJson(runId, progress.Records, progress.RecordTotal);
        await writer.UpdateAsync(runId, r => r.Status == CollectRunStatus.Running, r =>
        {
            progress.WriteCounts(r);
            r.ParseOutput = parseOutput;
            writer.End(r, status, errorCode, message);
        }, ct);
    }

    /// <summary>选中的快照按"运行记录 + 第几次请求"分组（一次请求的全部响应是解析器的一次输入），组内按序号排序</summary>
    private async Task<List<List<Snapshot>>> LoadGroupsAsync(IReadOnlyList<long> snapshotIds, CancellationToken ct)
    {
        var ids = snapshotIds.ToList();
        var selected = await db.Snapshots.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        return selected
            .GroupBy(s => (s.RunId, s.Attempt))
            .OrderBy(g => g.Min(s => s.Id))
            .Select(g => g.OrderBy(s => s.Seq).ToList())
            .ToList();
    }

    private async Task ProcessGroupAsync(
        long runId, CollectTask task, IParser parser, ICollectIngestor ingestor, List<Snapshot> group, Progress progress, CancellationToken ct)
    {
        progress.InvalidIds.AddRange(group.Where(s => !s.IsValid).Select(s => s.Id));
        var valid = group.Where(s => s.IsValid).ToList();
        if (valid.Count == 0)
        {
            return;
        }

        var validIds = valid.Select(s => s.Id).ToList();
        if (valid.FirstOrDefault(s => s.HttpStatus is < 200 or >= 300) is { } failedResponse)
        {
            progress.Fail(validIds, $"HTTP {failedResponse.HttpStatus} 的响应不解析");
            return;
        }

        var responses = new List<FetchedResponse>();
        foreach (var snapshot in valid)
        {
            if (await contents.GetAsync(snapshot.ContentHash, ct) is not { } body)
            {
                progress.Fail(validIds, $"快照 {snapshot.Id} 的内容已被清理");
                return;
            }

            responses.Add(new FetchedResponse(new Uri(snapshot.Url), snapshot.HttpStatus, snapshot.ContentType, body)
            {
                RequestKey = snapshot.RequestKey,
                ReceivedAt = snapshot.FetchedAt,
            });
        }

        ParseResult parsed;
        try
        {
            parsed = parser.Parse(new FetchResult(responses), task.Params);
        }
        catch (InvalidSnapshotException ex)
        {
            // 解析器判定为拦截页等：标记为无效，以后不再参与重新解析（ERR-PARSE-003）
            await snapshots.MarkInvalidAsync(validIds, ex.Message, ct);
            progress.InvalidIds.AddRange(validIds);
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "运行记录 {RunId}（重新解析）快照 {SnapshotIds} 解析失败", runId, validIds);
            progress.Fail(validIds, ex.Message);
            return;
        }

        try
        {
            var ingested = await ingestor.IngestAsync(new IngestRequest(runId, task.Id, valid[0].SourceId, parsed.Records, validIds), ct);
            progress.Add(parsed, ingested);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 入库失败时事务已回滚；清掉跟踪，接下来改运行记录时不会把残留的改动一起保存
            db.ChangeTracker.Clear();
            logger.LogError(ex, "运行记录 {RunId}（重新解析）快照 {SnapshotIds} 入库失败", runId, validIds);
            progress.Fail(validIds, $"入库失败：{ex.Message}");
        }
    }

    /// <summary>累计的条数、无效和失败的快照、解析输出</summary>
    private sealed class Progress
    {
        private int _inserted;
        private int _updated;
        private int _skipped;
        private int _locked;
        private int _failedRecords;
        private int _succeededGroups;
        private readonly List<string> _missing = [];
        private readonly List<string> _failures = [];

        public List<long> InvalidIds { get; } = [];

        public List<long> FailedIds { get; } = [];

        public List<object> Records { get; } = [];

        public int RecordTotal { get; private set; }

        public void Add(ParseResult parsed, IngestResult ingested)
        {
            _inserted += ingested.Inserted;
            _updated += ingested.Updated;
            _skipped += ingested.Skipped;
            _locked += ingested.Locked;
            _failedRecords += ingested.Failed;
            _missing.AddRange(parsed.Missing);
            _succeededGroups++;
            RecordTotal += parsed.Records.Count;
            Records.AddRange(parsed.Records.Take(CollectRunWriter.MaxParseOutputRecords - Records.Count));
        }

        public void Fail(IReadOnlyList<long> snapshotIds, string reason)
        {
            FailedIds.AddRange(snapshotIds);
            _failures.Add(reason);
        }

        public void WriteCounts(CollectRun run)
        {
            run.InsertedCount = _inserted;
            run.UpdatedCount = _updated;
            run.SkippedCount = _skipped;
            run.LockedCount = _locked;
        }

        /// <summary>结束状态：没有一组成功为失败；有无效快照、失败的组、缺失项或入库失败的记录为部分成功</summary>
        public (CollectRunStatus Status, string? ErrorCode, string? Message) Outcome(bool nothingSelected)
        {
            if (nothingSelected)
            {
                return (CollectRunStatus.Failed, null, "选中的快照都已不存在（可能已过保留期被清理）");
            }

            var problems = new List<string>();
            if (InvalidIds.Count > 0)
            {
                problems.Add($"无效快照 {InvalidIds.Count} 个，已跳过（{CollectErrorCodes.InvalidSnapshot}）：{List(InvalidIds)}");
            }

            if (FailedIds.Count > 0)
            {
                problems.Add($"解析或入库失败的快照 {FailedIds.Count} 个：{List(FailedIds)}；原因：{string.Join("；", _failures.Distinct().Take(MaxListedIds))}");
            }

            if (_missing.Count > 0)
            {
                problems.Add($"缺少 {_missing.Count} 项：{string.Join("、", _missing.Distinct().Take(MaxListedIds))}");
            }

            if (_failedRecords > 0)
            {
                problems.Add($"入库失败 {_failedRecords} 条");
            }

            if (_succeededGroups == 0)
            {
                var code = FailedIds.Count > 0 ? CollectErrorCodes.ParseFailed : CollectErrorCodes.InvalidSnapshot;
                return (CollectRunStatus.Failed, code, string.Join("；", problems));
            }

            return problems.Count > 0
                ? (CollectRunStatus.PartiallySucceeded, CollectErrorCodes.PartialData, string.Join("；", problems))
                : (CollectRunStatus.Succeeded, null, null);
        }

        private static string List(List<long> ids)
            => string.Join("、", ids.Take(MaxListedIds)) + (ids.Count > MaxListedIds ? $" 等 {ids.Count} 个" : string.Empty);
    }
}
