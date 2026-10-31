using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Suanpan.Collector.Execution;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Ops.Enums;
using Suanpan.Data;
using Suanpan.Infrastructure.Http;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Calendar.Watch;
using Suanpan.Services.Collect.Ingest;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Collect.Snapshots.Models;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Collect.Watch;
using Suanpan.Services.Collect.Watch.Models;
using Suanpan.Services.Common.Alerts;
using Suanpan.Services.Sys.Security;

namespace Suanpan.Collector.Watch;

/// <summary>
/// 一路盯盘（采集设计 §5.7，FR-003）：同一数据源、同一公布值任务、同一排期时间的事件只发一路请求。
/// 分三步，<see cref="RunAsync"/> 按时间把它们串起来，测试中可以按给定的时间逐步调用：
/// <list type="number">
/// <item><see cref="BeginAsync"/>：创建（服务重启后接着用）这一路的运行记录，事件改为"盯盘中"</item>
/// <item><see cref="PollAsync"/>：请求一次；内容有变化才存快照、解析、入库（BR-015、BR-016）；拿到值的事件退出，T+10 分钟改为延迟，
/// 被限流时间隔加倍</item>
/// <item><see cref="EndAsync"/>：没拿到值的改为延迟，按结果结束运行记录</item>
/// </list>
/// 每一步用一个新的依赖注入作用域；服务被取消（失去租约、停机）时不结束运行记录，由接手的实例接着写
/// </summary>
public sealed class WatchSession(WatchGroup group, IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger logger)
{
    private readonly HashSet<long> _pending = group.Events.Select(e => e.EventId).ToHashSet();
    private readonly HashSet<long> _done = [];
    private readonly Dictionary<string, string> _lastHashes = new(StringComparer.Ordinal);
    private bool _delayed;
    private bool _throttleAlerted;
    private int _attempt;
    private string? _lastErrorCode;
    private string? _lastError;

    public WatchGroup Group => group;

    /// <summary>这一路的运行记录；BeginAsync 之前为 0</summary>
    public long RunId { get; private set; }

    /// <summary>当前的请求间隔：被限流时加倍，成功一次恢复（ERR-COL-005）</summary>
    public TimeSpan Interval { get; private set; } = group.Timing.Interval;

    /// <summary>还没拿到值、仍在盯的事件</summary>
    public IReadOnlyCollection<long> Pending => _pending;

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>等到开始时间（T − 提前），请求到全部拿到值或窗口结束；被取消时直接退出，不结束运行记录</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        await DelayUntilAsync(group.Window.Start, ct);
        if (!await BeginAsync(ct))
        {
            return;
        }

        WatchStop? stop = null;
        while (_pending.Count > 0 && Now < group.Window.End)
        {
            var started = Now;
            stop = await PollAsync(ct);
            if (stop is not null || _pending.Count == 0)
            {
                break;
            }

            // 下一次请求：从这次开始算一个间隔；延迟判定的时刻先到时在那时醒来（BR-017）
            var next = started + Interval;
            if (!_delayed && group.Window.DelayAt < next)
            {
                next = group.Window.DelayAt;
            }

            await DelayUntilAsync(next < group.Window.End ? next : group.Window.End, ct);
        }

        await EndAsync(stop, ct);
    }

    /// <summary>
    /// 创建这一路的运行记录（触发方式"盯盘"，直接为运行中，scheduled_for 为排期时间）；已有运行中的记录（服务重启）时接着用，
    /// 已经结束时不再盯，返回 false。然后把待公布的事件改为"盯盘中"
    /// </summary>
    public async Task<bool> BeginAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SuanpanDbContext>();
        var now = Now;
        var inserted = await db.Database.SqlQuery<long>($"""
            INSERT INTO col_run (task_id, source_id, "trigger", scheduled_for, status, queued_at, started_at, heartbeat_at,
                                 attempt_count, inserted_count, updated_count, skipped_count, locked_count, attempts)
            VALUES ({group.TaskId}, {group.SourceId}, {(short)CollectRunTrigger.Watch}, {group.ScheduledAt}, {(short)CollectRunStatus.Running},
                    {now}, {now}, {now}, 0, 0, 0, 0, 0, '[]'::jsonb)
            ON CONFLICT (task_id, "trigger", scheduled_for) WHERE scheduled_for IS NOT NULL DO NOTHING
            RETURNING id AS "Value"
            """).ToListAsync(ct);
        if (inserted.Count > 0)
        {
            RunId = inserted[0];
        }
        else
        {
            var existing = await db.CollectRuns.AsNoTracking()
                .FirstAsync(r => r.TaskId == group.TaskId && r.Trigger == CollectRunTrigger.Watch && r.ScheduledFor == group.ScheduledAt, ct);
            if (existing.Status != CollectRunStatus.Running)
            {
                logger.LogInformation("任务 {TaskId} 在 {ScheduledAt:O} 的盯盘已经结束（运行记录 {RunId}），不再盯", group.TaskId, group.ScheduledAt, existing.Id);
                return false;
            }

            // 服务重启后接着写原来的运行记录（ERR-COL-006）
            RunId = existing.Id;
            _attempt = existing.AttemptCount;
            logger.LogWarning("盯盘服务重启后恢复任务 {TaskId} 在 {ScheduledAt:O} 的盯盘（运行记录 {RunId}）", group.TaskId, group.ScheduledAt, RunId);
        }

        await scope.ServiceProvider.GetRequiredService<ICalendarWatchService>().StartAsync(_pending, group.SourceId, RunId, ct);
        return true;
    }

    /// <summary>请求一次并处理结果；需要停止时返回原因（总开关关闭、数据源或任务停用、每日上限用完、任务参数不正确）</summary>
    public async Task<WatchStop?> PollAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<SuanpanDbContext>();
        var calendar = services.GetRequiredService<ICalendarWatchService>();

        var settings = await services.GetRequiredService<IWatchPlanService>().GetSettingsAsync(ct);
        var source = await db.DataSources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == group.SourceId, ct);
        var task = await db.CollectTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == group.TaskId, ct);
        if (!settings.Enabled)
        {
            return new WatchStop(null, "盯盘总开关已关闭");
        }

        if (source is not { IsEnabled: true } || task is not { IsEnabled: true })
        {
            return new WatchStop(null, source is not { IsEnabled: true } ? "数据源已停用或不存在" : "公布值采集任务已停用或不存在");
        }

        await RefreshAsync(calendar, ct);
        if (_pending.Count == 0)
        {
            return null;
        }

        // 延迟（BR-017，ERR-EVT-001）：T+10 分钟仍在盯盘中的改为延迟并告警，本路继续请求到窗口结束
        var now = Now;
        if (!_delayed && now >= group.Window.DelayAt)
        {
            await calendar.DelayAsync(_pending, group.SourceId, RunId, ct);
            _delayed = true;
        }

        var parser = services.GetServices<IParser>().FirstOrDefault(p => p.Key == task.ParserKey);
        var adapterKey = task.ParserKey[..Math.Max(task.ParserKey.LastIndexOf('.'), 0)];
        var adapter = services.GetServices<ICollectAdapter>().FirstOrDefault(a => a.Key == adapterKey);
        var ingestor = services.GetServices<ICollectIngestor>().FirstOrDefault(i => i.TargetType == task.TargetType);
        if (parser is null || adapter is null || ingestor is null)
        {
            return new WatchStop(null, $"缺少解析器 {task.ParserKey}、适配器或入库服务的实现");
        }

        var attempt = new CollectAttempt { No = (short)++_attempt, StartedAt = now };
        var started = timeProvider.GetTimestamp();
        var credential = source.Credential is { } encrypted ? services.GetRequiredService<ISecretProtector>().Decrypt(encrypted) : null;
        var context = new FetchContext(source, credential, task.Params, services.GetRequiredService<IHttpClientFactory>().CreateClient(CollectHttp.ClientName),
            services.GetRequiredService<IRateLimiter>(), services.GetRequiredService<IDataSourceDailyQuota>(), timeProvider);

        WatchStop? stop = null;
        IngestResult? ingested = null;
        string? parseOutput = null;
        try
        {
            var fetched = await adapter.FetchAsync(context, ct);
            var failed = fetched.Responses.FirstOrDefault(r => !r.IsSuccess);
            attempt.HttpStatus = (failed ?? fetched.Responses.LastOrDefault())?.StatusCode;
            var normal = group.Timing.Interval;
            Interval = WatchRule.NextInterval(Interval, normal, attempt.HttpStatus);
            if (failed is not null)
            {
                Fail(attempt, CollectErrorCodes.RequestFailed, $"HTTP {failed.StatusCode}：{failed.Url.GetLeftPart(UriPartial.Path)}");
                if (failed.StatusCode is 429 or 403)
                {
                    await AlertThrottledAsync(services, source.Name, failed.StatusCode, ct);
                }
            }

            // 只有内容变化才存快照、解析（BR-015、BR-016）：与这一路上一次的内容比较，服务重启后第一次一律当作有变化
            attempt.Changed = Changed(fetched);
            if (attempt.Changed == true)
            {
                var snapshotIds = await SaveSnapshotsAsync(services, fetched, ct);
                if (failed is null)
                {
                    (ingested, parseOutput) = await ParseAndIngestAsync(services, task, parser, ingestor, fetched, snapshotIds, attempt, ct);
                }
            }
        }
        catch (DailyLimitExceededException ex)
        {
            Fail(attempt, CollectErrorCodes.DailyLimitExceeded, ex.Message);
            stop = new WatchStop(CollectErrorCodes.DailyLimitExceeded, ex.Message);
        }
        catch (FetchParametersException ex)
        {
            Fail(attempt, CollectErrorCodes.RequestFailed, $"任务参数不正确：{ex.Message}");
            stop = new WatchStop(CollectErrorCodes.RequestFailed, $"任务参数不正确：{ex.Message}");
        }
        catch (Exception ex) when (ex is FetchTimeoutException or HttpRequestException)
        {
            // 超时、网络错误：只记在请求记录中，按原间隔继续，不重试、不退避（§5.7 第 7 步）
            attempt.HttpStatus = (int?)(ex as HttpRequestException)?.StatusCode;
            Fail(attempt, CollectErrorCodes.RequestFailed, ex.Message);
        }

        if (attempt.Error is null)
        {
            // 最后一次请求成功时，结束的原因按官方延迟算
            (_lastErrorCode, _lastError) = (null, null);
        }

        attempt.DurationMs = (int)timeProvider.GetElapsedTime(started).TotalMilliseconds;
        attempt.DeferredMs = context.DeferredMs;
        await services.GetRequiredService<CollectRunWriter>().UpdateAsync(RunId, r => r.Status == CollectRunStatus.Running, r =>
        {
            r.AttemptCount = (short)_attempt;
            r.Attempts.Add(attempt);
            r.HeartbeatAt = now;
            if (ingested is not null)
            {
                r.InsertedCount += ingested.Inserted;
                r.UpdatedCount += ingested.Updated;
                r.SkippedCount += ingested.Skipped;
                r.LockedCount += ingested.Locked;
                r.ParseOutput = parseOutput ?? r.ParseOutput;
            }
        }, ct);

        // 入库后立刻看哪些事件拿到值了，不等下一次请求（FR-003 验收第 3 条）
        if (ingested is not null)
        {
            await RefreshAsync(calendar, ct);
        }

        return stop;
    }

    /// <summary>
    /// 结束（§5.7 第 9 步）：还没拿到值的事件改为延迟（窗口短于 10 分钟、或提前停止时），运行记录按结果结束：
    /// 全部拿到为成功，部分拿到为部分成功，一个都没有为失败（官方延迟，或停止的原因、最后一次请求的错误）
    /// </summary>
    public async Task EndAsync(WatchStop? stop, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var calendar = services.GetRequiredService<ICalendarWatchService>();
        await RefreshAsync(calendar, ct);
        if (_pending.Count > 0)
        {
            await calendar.DelayAsync(_pending, group.SourceId, RunId, ct);
        }

        var missing = group.Events.Where(e => _pending.Contains(e.EventId)).Select(e => $"{e.IndicatorCode} {e.ReferencePeriod}").ToList();
        var (status, code, message) = (_pending.Count, _done.Count) switch
        {
            (0, _) => (CollectRunStatus.Succeeded, (string?)null, (string?)null),
            (_, > 0) => (CollectRunStatus.PartiallySucceeded, CollectErrorCodes.OfficialDelay, $"没拿到值：{string.Join("、", missing)}"),
            _ when stop is not null => (CollectRunStatus.Failed, stop.ErrorCode, stop.Reason),
            _ => (CollectRunStatus.Failed, _lastErrorCode ?? CollectErrorCodes.OfficialDelay,
                _lastErrorCode is null ? $"窗口结束仍没拿到值（官方延迟）：{string.Join("、", missing)}" : _lastError),
        };
        if (stop is not null && status != CollectRunStatus.Failed)
        {
            message = $"{stop.Reason}；{message}".TrimEnd('；');
        }

        var writer = services.GetRequiredService<CollectRunWriter>();
        await writer.UpdateAsync(RunId, r => r.Status == CollectRunStatus.Running, r => writer.End(r, status, code, message), ct);
    }

    /// <summary>
    /// 重新读这一路的事件：拿到值的退出（WatchRule.IsDone），取消的退出，被官方改期（排期时间或精度变了）的退出并改回待公布
    /// </summary>
    private async Task RefreshAsync(ICalendarWatchService calendar, CancellationToken ct)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        foreach (var e in await calendar.GetEventsAsync(_pending, ct))
        {
            if (WatchRule.IsDone(e))
            {
                _pending.Remove(e.Id);
                _done.Add(e.Id);
            }
            else if (e.Status == CalendarEventStatus.Cancelled)
            {
                _pending.Remove(e.Id);
            }
            else if (e.ScheduledAt != group.ScheduledAt || e.TimePrecision != TimePrecision.Minute)
            {
                _pending.Remove(e.Id);
                await calendar.ReturnToScheduledAsync(e.Id, group.SourceId, RunId, ct);
            }
        }
    }

    /// <summary>这次的响应与这一路上一次相比内容有没有变化：按请求标识比较内容哈希</summary>
    private bool Changed(FetchResult fetched)
    {
        var changed = false;
        foreach (var response in fetched.Responses)
        {
            var key = response.RequestKey ?? response.Url.GetLeftPart(UriPartial.Path);
            var hash = Convert.ToHexStringLower(SHA256.HashData(response.Body));
            if (!_lastHashes.TryGetValue(key, out var last) || last != hash)
            {
                changed = true;
            }

            _lastHashes[key] = hash;
        }

        return changed;
    }

    private async Task<IReadOnlyList<long>> SaveSnapshotsAsync(IServiceProvider services, FetchResult fetched, CancellationToken ct)
    {
        var now = Now;
        var inputs = fetched.Responses
            .Select(r => new SnapshotInput(r.Url.GetLeftPart(UriPartial.Path), r.RequestKey ?? r.Url.GetLeftPart(UriPartial.Path), r.StatusCode,
                r.ContentType, r.Body, r.ReceivedAt ?? now))
            .ToList();
        return await services.GetRequiredService<ISnapshotService>()
            .SaveAsync(new SnapshotBatch(RunId, (short)_attempt, group.TaskId, group.SourceId, inputs), skipUnchanged: true, ct);
    }

    /// <summary>解析、入库；解析失败、快照无效、入库失败只记在这次请求中，按原间隔继续</summary>
    private async Task<(IngestResult? Ingested, string? ParseOutput)> ParseAndIngestAsync(
        IServiceProvider services, CollectTask task, IParser parser, ICollectIngestor ingestor, FetchResult fetched, IReadOnlyList<long> snapshotIds,
        CollectAttempt attempt, CancellationToken ct)
    {
        ParseResult parsed;
        try
        {
            parsed = parser.Parse(fetched, task.Params);
        }
        catch (InvalidSnapshotException ex)
        {
            await services.GetRequiredService<ISnapshotService>().MarkInvalidAsync(snapshotIds, ex.Message, ct);
            Fail(attempt, CollectErrorCodes.InvalidSnapshot, ex.Message);
            return (null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "盯盘的运行记录 {RunId}（解析器 {ParserKey}）解析失败", RunId, task.ParserKey);
            Fail(attempt, CollectErrorCodes.ParseFailed, ex.Message);
            return (null, null);
        }

        var parseOutput = services.GetRequiredService<CollectRunWriter>().ParseOutputJson(RunId, parsed.Records);
        try
        {
            return (await ingestor.IngestAsync(new IngestRequest(RunId, task.Id, group.SourceId, parsed.Records, snapshotIds), ct), parseOutput);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "盯盘的运行记录 {RunId} 入库失败", RunId);
            Fail(attempt, null, $"入库失败：{ex.Message}");
            return (null, parseOutput);
        }
    }

    /// <summary>本路第一次被限流时告警（重要，对象为数据源，ERR-COL-005）；告警失败只记日志</summary>
    private async Task AlertThrottledAsync(IServiceProvider services, string sourceName, int status, CancellationToken ct)
    {
        if (_throttleAlerted)
        {
            return;
        }

        _throttleAlerted = true;
        try
        {
            await services.GetRequiredService<IAlertService>().RaiseAsync(AlertLevel.Important, sourceName,
                $"盯盘被限流（HTTP {status}），请求间隔调整为 {Interval.TotalSeconds:0} 秒（ERR-COL-005）", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "告警发送失败：{Subject} 盯盘被限流", sourceName);
        }
    }

    private void Fail(CollectAttempt attempt, string? errorCode, string error)
    {
        attempt.Error = error;
        (_lastErrorCode, _lastError) = (errorCode, error);
    }

    private async Task DelayUntilAsync(DateTime at, CancellationToken ct)
    {
        var wait = at - Now;
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, timeProvider, ct);
        }
    }
}

/// <summary>提前停止一路盯盘的原因；ErrorCode 为运行记录的错误码（为空时按结果决定）</summary>
public sealed record WatchStop(string? ErrorCode, string Reason);
