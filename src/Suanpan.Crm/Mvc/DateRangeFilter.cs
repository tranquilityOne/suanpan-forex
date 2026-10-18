namespace Suanpan.Crm.Mvc;

/// <summary>
/// 列表页的日期范围筛选：页面上按操作者时区选起止日期（包含两端），换算成 UTC 查询区间（开始含、结束不含）。
/// 库里的时间都是 UTC（前端约定 §7.2）。
/// </summary>
public static class DateRangeFilter
{
    public static (DateTime? Begin, DateTime? End) ToUtc(DateOnly? from, DateOnly? to, TimeZoneInfo timeZone)
        => (from is { } start ? StartOfDayUtc(start, timeZone) : null,
            to is { } end ? StartOfDayUtc(end.AddDays(1), timeZone) : null);

    private static DateTime StartOfDayUtc(DateOnly date, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), timeZone);
}
