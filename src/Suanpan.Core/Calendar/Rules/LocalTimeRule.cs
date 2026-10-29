using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Reference.Rules;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 发布机构当地时间换算成 UTC（BR-037，后端约定 §10）：时区用 IANA 名称（如 America/New_York、Europe/London），
/// 夏令时由系统的 IANA 时区库计算。夏令时切换当天的两种特殊时刻：
/// <list type="bullet">
/// <item>不存在的时刻（时钟往前跳过的那段，如纽约 3 月 02:30）：按跳过的时长顺延，即按切换前的偏移换算（02:30 按 03:30 计）</item>
/// <item>重复出现的时刻（时钟往回拨的那段，如纽约 11 月 01:30）：取较早的一次，即按夏令时的偏移换算</item>
/// </list>
/// 换算结果标明属于哪种情况；排期采集只在事件上标记，不需要人工复核（BR-037）
/// </summary>
public static class LocalTimeRule
{
    /// <param name="ianaTimeZone">发布机构所在经济体的时区（IANA 名称，经济体保存时已由 TimeZoneRule 校验）</param>
    /// <exception cref="ArgumentException">不是合法的 IANA 时区</exception>
    public static LocalTimeConversion ToUtc(DateOnly date, TimeOnly time, string ianaTimeZone)
    {
        if (!TimeZoneRule.IsValid(ianaTimeZone))
        {
            throw new ArgumentException($"不是合法的 IANA 时区：{ianaTimeZone}", nameof(ianaTimeZone));
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZone);
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // 切换前一天的偏移就是跳过之前的偏移（同一天内不会切换两次）
            var offsetBeforeGap = zone.GetUtcOffset(local.AddDays(-1));
            return new LocalTimeConversion(Utc(local - offsetBeforeGap), LocalTimeResolution.ShiftedForward);
        }

        if (zone.IsAmbiguousTime(local))
        {
            // 较早的一次按夏令时计，夏令时的偏移较大（如纽约 -4 比 -5 大）
            var daylightOffset = zone.GetAmbiguousTimeOffsets(local).Max();
            return new LocalTimeConversion(Utc(local - daylightOffset), LocalTimeResolution.Ambiguous);
        }

        return new LocalTimeConversion(TimeZoneInfo.ConvertTimeToUtc(local, zone), LocalTimeResolution.Exact);
    }

    /// <summary>某个 UTC 时刻在机构当地是哪一天（如判断"仅日期"的排期是否已经过去，日历设计 §4.2）</summary>
    /// <exception cref="ArgumentException">不是合法的 IANA 时区</exception>
    public static DateOnly LocalDate(DateTime utc, string ianaTimeZone)
    {
        if (!TimeZoneRule.IsValid(ianaTimeZone))
        {
            throw new ArgumentException($"不是合法的 IANA 时区：{ianaTimeZone}", nameof(ianaTimeZone));
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

/// <summary>换算结果：UTC 时间（Kind 为 Utc）以及当地时刻属于哪种情况</summary>
public sealed record LocalTimeConversion(DateTime Utc, LocalTimeResolution Resolution);
