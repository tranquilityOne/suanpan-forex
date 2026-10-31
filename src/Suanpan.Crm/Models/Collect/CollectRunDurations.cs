using System.Globalization;

namespace Suanpan.Crm.Models.Collect;

/// <summary>运行记录的耗时显示：不到 1 分钟显示秒（一位小数），否则显示分和秒；没有结束时返回空。盯盘的入库时延也按它显示</summary>
public static class CollectRunDurations
{
    public static string? Format(DateTime? startedAt, DateTime? endedAt)
        => startedAt is { } start && endedAt is { } end ? Format(end - start) : null;

    public static string Format(TimeSpan duration)
        => duration.TotalMinutes < 1
            ? duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒"
            : $"{(int)duration.TotalMinutes} 分 {duration.Seconds} 秒";
}
