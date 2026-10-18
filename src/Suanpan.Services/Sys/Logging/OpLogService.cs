using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data;
using Suanpan.Services.Common.Csv;
using Suanpan.Services.Common.Querying;
using Suanpan.Services.Sys.Logging.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Sys.Logging;

public sealed class OpLogService(SuanpanDbContext db, TimeProvider timeProvider) : IOpLogService
{
    public async Task AddAsync(SysOpLog log, CancellationToken ct = default)
    {
        var createdAt = timeProvider.GetUtcNow().UtcDateTime;

        // 用一条 INSERT 直接写入，不经过变更跟踪：业务操作失败时，上下文里可能留有未保存的改动，不能随日志一起保存
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO sys_op_log (user_id, username, roles, module, action, perm_code, http_method, request_url,
                request_params, reason, is_success, error_msg, duration_ms, ip, user_agent, trace_id, created_at)
            VALUES ({log.UserId}, {LogExport.Cut(log.Username, 64)}, {LogExport.Cut(log.Roles, 256)},
                {LogExport.Cut(log.Module, 64)}, {LogExport.Cut(log.Action, 64)}, {LogExport.Cut(log.PermCode, 128)},
                {LogExport.Cut(log.HttpMethod, 8)}, {LogExport.Cut(log.RequestUrl, 256)}, {log.RequestParams},
                {LogExport.Cut(log.Reason, 512)}, {log.IsSuccess}, {LogExport.Cut(log.ErrorMsg, 512)}, {log.DurationMs},
                {LogExport.Cut(log.Ip, 64)}, {LogExport.Cut(log.UserAgent, 256)}, {LogExport.Cut(log.TraceId, 64)}, {createdAt})
            """, ct);
    }

    public Task<PagedResult<OpLogRow>> QueryAsync(OpLogQuery query, CancellationToken ct = default)
        => Filter(query)
            .OrderByDescending(x => x.Id)
            .Select(x => new OpLogRow(
                x.Id, x.CreatedAt, x.UserId, x.Username, x.Roles, x.Module, x.Action, x.PermCode, x.HttpMethod,
                x.RequestUrl, x.RequestParams, x.Reason, x.IsSuccess, x.ErrorMsg, x.DurationMs, x.Ip, x.UserAgent, x.TraceId))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public async Task<int> ExportAsync(OpLogQuery query, Stream output, TimeZoneInfo timeZone, CancellationToken ct = default)
    {
        await using var csv = new CsvWriter(output);
        await csv.WriteRowAsync(
            LogExport.TimeHeader(timeZone), "登录名", "角色", "模块", "动作", "权限码", "结果", "失败原因", "原因",
            "耗时（毫秒）", "IP", "请求方法", "请求地址", "请求参数", "TraceId");

        var count = 0;
        var logs = Filter(query).OrderByDescending(x => x.Id).Take(LogExport.MaxRows).AsAsyncEnumerable();
        await foreach (var x in logs.WithCancellation(ct))
        {
            await csv.WriteRowAsync(
                LogExport.FormatTime(x.CreatedAt, timeZone), x.Username, x.Roles, x.Module, x.Action, x.PermCode,
                x.IsSuccess ? "成功" : "失败", x.ErrorMsg, x.Reason, x.DurationMs.ToString(CultureInfo.InvariantCulture), x.Ip, x.HttpMethod,
                x.RequestUrl, x.RequestParams, x.TraceId);
            count++;
        }

        return count;
    }

    private IQueryable<SysOpLog> Filter(OpLogQuery query)
    {
        var logs = db.SysOpLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            var pattern = LikePattern.Contains(query.Username);
            logs = logs.Where(x => EF.Functions.ILike(x.Username, pattern, LikePattern.Escape));
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            var module = query.Module.Trim();
            logs = logs.Where(x => x.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            logs = logs.Where(x => x.Action == action);
        }

        if (query.IsSuccess is { } isSuccess)
        {
            logs = logs.Where(x => x.IsSuccess == isSuccess);
        }

        if (query.Begin is { } begin)
        {
            logs = logs.Where(x => x.CreatedAt >= begin);
        }

        if (query.End is { } end)
        {
            logs = logs.Where(x => x.CreatedAt < end);
        }

        return logs;
    }
}
