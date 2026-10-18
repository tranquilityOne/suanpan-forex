using System.Globalization;

namespace Suanpan.Services.Sys.Logging;

/// <summary>日志导出（sys 设计 §7.3）：按当前筛选条件导出 CSV，单次最多 <see cref="MaxRows"/> 行，时间按操作者时区显示</summary>
public static class LogExport
{
    public const int MaxRows = 100_000;

    public static string TimeHeader(TimeZoneInfo timeZone) => $"时间（{timeZone.Id}）";

    public static string FormatTime(DateTime utc, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>截断到列的长度上限，避免超长输入导致日志写入失败</summary>
    public static string? Cut(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];
}
