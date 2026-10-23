using Cronos;

namespace Suanpan.Services.Collect.Tasks;

/// <summary>
/// 采集任务的 cron（采集设计 §3.2、§4.2，BR-008）：只接受 5 段标准写法（分 时 日 月 周），所以最小间隔天然是 1 分钟；
/// 一律按 UTC 解释（后端约定 §8.3）。基于 Cronos 库，放在 Services 是因为 Core 不引用第三方包。
/// </summary>
public static class CronSchedule
{
    /// <summary>写法正确时返回 null，否则返回面向用户的提示（ERR-COL-001）</summary>
    public static string? Validate(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            return "请填写 cron";
        }

        try
        {
            CronExpression.Parse(cron.Trim(), CronFormat.Standard);
            return null;
        }
        catch (CronFormatException)
        {
            return "cron 必须是 5 段写法（分 时 日 月 周），按 UTC 解释，如 */5 * * * * 或 0 1 * * *；不支持秒";
        }
    }

    /// <summary>
    /// (fromExclusive, toInclusive] 之间最后一个时间点；没有时返回 null。调度分发用它判断任务是否到点，
    /// 错过的多个时间点只取最近一次（采集设计 §4.2）。时间都是 UTC
    /// </summary>
    public static DateTime? LatestOccurrence(string cron, DateTime fromExclusive, DateTime toInclusive)
        => Parse(cron).GetOccurrences(Utc(fromExclusive), Utc(toInclusive), TimeZoneInfo.Utc, fromInclusive: false, toInclusive: true)
            .Select(occurrence => (DateTime?)occurrence)
            .LastOrDefault();

    /// <summary>from 之后的若干个时间点（UTC），表单上预览下几次执行时间</summary>
    public static IReadOnlyList<DateTime> NextOccurrences(string cron, DateTime fromExclusive, int count)
    {
        var expression = Parse(cron);
        var occurrences = new List<DateTime>(count);
        var from = Utc(fromExclusive);
        while (occurrences.Count < count && expression.GetNextOccurrence(from, TimeZoneInfo.Utc) is { } next)
        {
            occurrences.Add(next);
            from = next;
        }

        return occurrences;
    }

    private static CronExpression Parse(string cron) => CronExpression.Parse(cron.Trim(), CronFormat.Standard);

    /// <summary>Cronos 要求 Kind 为 Utc；从数据库读出的 timestamptz 已是 Utc</summary>
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
