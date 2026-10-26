using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Data;
using Suanpan.Services.Collect.Tasks.Models;
using Suanpan.Services.Common.Querying;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Collect.Tasks;

public sealed class CollectTaskService(SuanpanDbContext db, IParserCatalog parsers, TimeProvider timeProvider) : ICollectTaskService
{
    /// <summary>表单上预览的执行次数（采集设计 §3.2）</summary>
    public const int PreviewCount = 3;

    public async Task<PagedResult<CollectTaskRow>> QueryAsync(CollectTaskQuery query, CancellationToken ct = default)
    {
        var tasks = db.CollectTasks.AsNoTracking();
        if (query.SourceId is { } sourceId)
        {
            tasks = tasks.Where(t => t.SourceId == sourceId);
        }

        if (query.TargetType is { } targetType)
        {
            tasks = tasks.Where(t => t.TargetType == targetType);
        }

        if (query.IsEnabled is { } isEnabled)
        {
            tasks = tasks.Where(t => t.IsEnabled == isEnabled);
        }

        var result = await (
            from t in tasks
            join s in db.DataSources on t.SourceId equals s.Id
            orderby t.Name
            select new CollectTaskRow(
                t.Id, t.Name, t.SourceId, s.Name, s.IsEnabled, t.ParserKey, t.TargetType, t.Cron, t.IsEnabled,
                db.CollectRuns.Where(r => r.TaskId == t.Id).OrderByDescending(r => r.QueuedAt).Select(r => (CollectRunStatus?)r.Status).FirstOrDefault(),
                db.CollectRuns.Where(r => r.TaskId == t.Id).Max(r => (DateTime?)r.QueuedAt)))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

        var now = Now;
        return result with
        {
            Items = result.Items
                .Select(row => row with { ParserName = parsers.Find(row.ParserKey)?.Name, NextRunAt = NextRunAt(row, now) })
                .ToList(),
        };
    }

    public Task<CollectTask?> GetAsync(long id, CancellationToken ct = default)
        => db.CollectTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<CollectTask>> GetAllAsync(CancellationToken ct = default)
        => await db.CollectTasks.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<ManageResult> CreateAsync(CollectTaskInput input, CancellationToken ct = default)
    {
        var task = new CollectTask { IsEnabled = false };
        if (await ApplyAndValidateAsync(task, input, ct) is { } invalid)
        {
            return invalid;
        }

        db.CollectTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> UpdateAsync(long id, CollectTaskInput input, CancellationToken ct = default)
    {
        if (await db.CollectTasks.FirstOrDefaultAsync(t => t.Id == id, ct) is not { } task)
        {
            return ManageResult.Fail("采集任务不存在");
        }

        // 校验失败时不保存，留在上下文里的改动随请求结束丢弃（操作日志用单独的 INSERT 写入，不会带上它们）
        var currentCron = task.Cron;
        if (await ApplyAndValidateAsync(task, input, ct) is { } invalid)
        {
            return invalid;
        }

        // 采集设计 §4.2：从现在起按新规则算，不因旧的记账时间补发一次
        if (task.Cron != currentCron)
        {
            task.LastDispatchedAt = Now;
        }

        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> SetEnabledAsync(long id, bool enabled, CancellationToken ct = default)
    {
        if (await db.CollectTasks.FirstOrDefaultAsync(t => t.Id == id, ct) is not { } task)
        {
            return ManageResult.Fail("采集任务不存在");
        }

        if (enabled && !task.IsEnabled)
        {
            var source = await db.DataSources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == task.SourceId, ct);
            if (source is not { IsEnabled: true })
            {
                return ManageResult.Fail($"数据源 {source?.Name} 已停用，请先启用数据源");
            }

            task.LastDispatchedAt = Now;
        }

        task.IsEnabled = enabled;
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default)
    {
        if (await db.CollectTasks.FirstOrDefaultAsync(t => t.Id == id, ct) is not { } task)
        {
            return ManageResult.Fail("采集任务不存在");
        }

        if (await db.CollectRuns.AnyAsync(r => r.TaskId == task.Id, ct))
        {
            return ManageResult.Fail($"采集任务 {task.Name} 已有运行记录，不能删除，可以停用");
        }

        db.CollectTasks.Remove(task);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public CronPreview PreviewSchedule(string? cron)
    {
        if (CronSchedule.Normalize(cron) is not { } normalized)
        {
            return new CronPreview(null, []);
        }

        return CronSchedule.Validate(normalized) is { } error
            ? new CronPreview(error, [])
            : new CronPreview(null, CronSchedule.NextOccurrences(normalized, Now, PreviewCount));
    }

    /// <summary>
    /// 把输入写到实体上再校验：字段规则（CollectTaskRule）、cron 写法（ERR-COL-001）、名称唯一、数据源存在。
    /// 目标数据类型从解析器目录带出（BR-012）
    /// </summary>
    private async Task<ManageResult?> ApplyAndValidateAsync(CollectTask task, CollectTaskInput input, CancellationToken ct)
    {
        task.Name = input.Name.Trim();
        task.SourceId = input.SourceId;
        task.ParserKey = input.ParserKey.Trim();
        task.TargetType = parsers.Find(task.ParserKey)?.TargetType ?? task.TargetType;
        task.Cron = CronSchedule.Normalize(input.Cron);
        task.Params = string.IsNullOrWhiteSpace(input.Params) ? null : input.Params.Trim();
        task.Remark = string.IsNullOrWhiteSpace(input.Remark) ? null : input.Remark.Trim();

        if (CollectTaskRule.Validate(task, parsers.Find) is { } error)
        {
            return ManageResult.Fail(error.Message, error.Field);
        }

        if (task.Cron is not null && CronSchedule.Validate(task.Cron) is { } cronError)
        {
            return ManageResult.Fail(cronError, nameof(CollectTaskInput.Cron));
        }

        if (await db.CollectTasks.AnyAsync(t => t.Name == task.Name && t.Id != task.Id, ct))
        {
            return ManageResult.Fail($"采集任务名称\"{task.Name}\"已存在", nameof(CollectTaskInput.Name));
        }

        return await db.DataSources.AnyAsync(s => s.Id == task.SourceId, ct)
            ? null
            : ManageResult.Fail("数据源不存在", nameof(CollectTaskInput.SourceId));
    }

    /// <summary>任务和数据源都启用、有 cron 时，下一个时间点</summary>
    private static DateTime? NextRunAt(CollectTaskRow row, DateTime now)
        => row is { IsEnabled: true, SourceEnabled: true, Cron: { } cron } && CronSchedule.Validate(cron) is null
            ? CronSchedule.NextOccurrences(cron, now, 1).FirstOrDefault()
            : null;

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
}
