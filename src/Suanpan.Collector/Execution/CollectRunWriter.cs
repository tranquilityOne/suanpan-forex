using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Data;

namespace Suanpan.Collector.Execution;

/// <summary>改运行记录的公共写法，采集（<see cref="CollectExecutor"/>）和重新解析（<see cref="ReparseExecutor"/>）共用</summary>
public sealed class CollectRunWriter(SuanpanDbContext db, TimeProvider timeProvider, ILogger<CollectRunWriter> logger)
{
    /// <summary>运行记录的 parse_output 最多存这么多条记录（采集设计 §2.1）</summary>
    public const int MaxParseOutputRecords = 1000;

    private const int MaxErrorMessageLength = 2000;

    private static readonly JsonSerializerOptions ParseOutputJsonOptions = new(JsonSerializerDefaults.Web);

    public DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// 在行锁内读出运行记录，满足条件时修改并保存（后端约定 §6.4）：与重复投递的指令、执行中断的回收互斥。
    /// 状态只能按 CollectRunStatusRule 流转
    /// </summary>
    public async Task<bool> UpdateAsync(long runId, Func<CollectRun, bool> guard, Action<CollectRun> change, CancellationToken ct)
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

    /// <summary>结束运行记录：写状态、结束时间、错误码和原因（原因超过 2000 字截断）</summary>
    public void End(CollectRun run, CollectRunStatus status, string? errorCode, string? errorMessage)
    {
        run.Status = status;
        run.EndedAt = Now;
        run.NextAttemptAt = null;
        run.ErrorCode = errorCode;
        run.ErrorMessage = errorMessage is { Length: > MaxErrorMessageLength } ? errorMessage[..MaxErrorMessageLength] : errorMessage;
    }

    /// <summary>
    /// 解析器输出的记录（最多前 1000 条）存进运行记录，快照详情中"当时的解析结果"用（FR-004 主流程第 2 步）；序列化失败时不存。
    /// total 为全部条数，默认就是 records 的条数
    /// </summary>
    public string? ParseOutputJson(long runId, IReadOnlyList<object> records, int? total = null)
    {
        try
        {
            return JsonSerializer.Serialize(new { total = total ?? records.Count, records = records.Take(MaxParseOutputRecords) }, ParseOutputJsonOptions);
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "运行记录 {RunId} 的解析结果无法序列化，不保存", runId);
            return null;
        }
    }
}
