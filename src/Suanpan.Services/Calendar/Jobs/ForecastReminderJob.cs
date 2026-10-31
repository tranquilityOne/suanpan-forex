using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Ops.Enums;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Common.Alerts;

namespace Suanpan.Services.Calendar.Jobs;

/// <summary>
/// 预期值的提醒（日历设计 §3.8，BR-056）：Job 中每 15 分钟执行的周期任务 calendar-forecast-reminder，
/// 高重要性、未公布、预期值为空、精确到分钟且排期在未来 24 小时内的事件，告警（级别"重要"）提醒编辑录入；
/// 每个事件只提醒一次（Redis 标记，48 小时过期），告警发送失败时清掉标记，下一次再提醒
/// </summary>
public sealed class ForecastReminderJob(
    SuanpanDbContext db,
    IRedisCache cache,
    IAlertService alerts,
    IDistributedLock distributedLock,
    TimeProvider timeProvider,
    ILogger<ForecastReminderJob> logger)
{
    public const string JobId = "calendar-forecast-reminder";

    public const string Cron = "*/15 * * * *";

    private const string LockKey = "calendar:forecast-reminder";

    private const string RemindedKeyPrefix = "calendar:forecast-reminded:";

    private static readonly TimeSpan LockExpiry = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan RemindedExpiry = TimeSpan.FromHours(48);

    private static readonly CalendarEventStatus[] UnreleasedStatuses =
        [CalendarEventStatus.Scheduled, CalendarEventStatus.Watching, CalendarEventStatus.Delayed, CalendarEventStatus.Unconfirmed];

    public static string RemindedKey(long eventId) => RemindedKeyPrefix + eventId;

    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken ct)
    {
        await using var handle = await distributedLock.AcquireAsync(LockKey, LockExpiry, ct: ct);
        if (handle is null)
        {
            logger.LogInformation("另一个实例正在检查待录入的预期值，跳过本次");
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var end = now.Add(ForecastRule.ReminderWindow);
        var events = await (
                from e in db.CalendarEvents.AsNoTracking()
                where UnreleasedStatuses.Contains(e.Status) && e.Actual == null && e.Forecast == null
                    && e.TimePrecision == TimePrecision.Minute && e.ScheduledAt > now && e.ScheduledAt <= end
                join i in db.Indicators on e.IndicatorCode equals i.Code
                where i.Importance == Importance.High
                orderby e.ScheduledAt, e.Id
                select new { e.Id, e.IndicatorCode, e.ReferencePeriod, e.ScheduledAt, i.ShortName })
            .ToListAsync(ct);

        var reminded = 0;
        foreach (var e in events)
        {
            var key = RemindedKey(e.Id);
            if (!await cache.SetIfNotExistsAsync(key, true, RemindedExpiry))
            {
                continue;
            }

            try
            {
                await alerts.RaiseAsync(AlertLevel.Important, "预期值录入",
                    $"{e.ShortName.Zh}（{e.IndicatorCode} {e.ReferencePeriod}）将于 {e.ScheduledAt:yyyy-MM-dd HH:mm} UTC 公布，尚未录入预期值", ct);
                reminded++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "事件 {EventId} 的预期值提醒发送失败，下一次再提醒", e.Id);
                await cache.RemoveAsync(key);
            }
        }

        if (reminded > 0)
        {
            logger.LogInformation("已提醒 {Count} 个未录入预期值的事件", reminded);
        }
    }
}
