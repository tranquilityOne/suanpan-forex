using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Common.Csv;
using Suanpan.Services.Common.Querying;
using Suanpan.Services.Sys.Logging.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Sys.Logging;

public sealed class LoginLogService(SuanpanDbContext db, TimeProvider timeProvider) : ILoginLogService
{
    public async Task AddAsync(SysLoginLog log, CancellationToken ct = default)
    {
        var createdAt = timeProvider.GetUtcNow().UtcDateTime;

        // 与操作日志相同：直接 INSERT，不随上下文中未保存的改动一起提交
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO sys_login_log (username, user_id, stage, is_success, fail_reason, ip, user_agent, created_at)
            VALUES ({LogExport.Cut(log.Username, 64)}, {log.UserId}, {(short)log.Stage}, {log.IsSuccess},
                {LogExport.Cut(log.FailReason, 128)}, {LogExport.Cut(log.Ip, 64)}, {LogExport.Cut(log.UserAgent, 256)}, {createdAt})
            """, ct);
    }

    public Task<PagedResult<LoginLogRow>> QueryAsync(LoginLogQuery query, CancellationToken ct = default)
        => Filter(query)
            .OrderByDescending(x => x.Id)
            .Select(x => new LoginLogRow(x.Id, x.CreatedAt, x.Username, x.UserId, x.Stage, x.IsSuccess, x.FailReason, x.Ip, x.UserAgent))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public async Task<int> ExportAsync(LoginLogQuery query, Stream output, TimeZoneInfo timeZone, CancellationToken ct = default)
    {
        await using var csv = new CsvWriter(output);
        await csv.WriteRowAsync(LogExport.TimeHeader(timeZone), "登录名", "步骤", "结果", "失败原因", "IP", "User-Agent");

        var count = 0;
        var logs = Filter(query).OrderByDescending(x => x.Id).Take(LogExport.MaxRows).AsAsyncEnumerable();
        await foreach (var x in logs.WithCancellation(ct))
        {
            await csv.WriteRowAsync(
                LogExport.FormatTime(x.CreatedAt, timeZone), x.Username, StageName(x.Stage),
                x.IsSuccess ? "成功" : "失败", x.FailReason, x.Ip, x.UserAgent);
            count++;
        }

        return count;
    }

    private IQueryable<SysLoginLog> Filter(LoginLogQuery query)
    {
        var logs = db.SysLoginLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            var pattern = LikePattern.Contains(query.Username);
            logs = logs.Where(x => EF.Functions.ILike(x.Username, pattern, LikePattern.Escape));
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

    /// <summary>步骤的显示名，页面和导出共用</summary>
    public static string StageName(LoginStage stage) => stage switch
    {
        LoginStage.Password => "密码",
        LoginStage.Totp => "TOTP",
        LoginStage.Activation => "激活",
        LoginStage.Logout => "退出",
        LoginStage.TotpSkipped => "跳过 TOTP（开发环境）",
        _ => stage.ToString(),
    };
}
