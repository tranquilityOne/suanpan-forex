using System.Globalization;
using System.Text.Json;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Reference.Rules;

namespace Suanpan.Crm.Models.Calendar;

/// <summary>
/// 日历事件数值和变更历史的显示（日历设计 §9.3）：按指标的小数位四舍五入（远离零，与入库相同，BR-043），
/// 区间型显示"下限 ~ 上限"；百分比带 %，指数不带单位，其他带中文单位名
/// </summary>
public static class EventValueFormat
{
    public static string? Format(decimal? value, decimal? upper, IndicatorUnit unit, short decimals)
    {
        if (value is not { } lower)
        {
            return null;
        }

        var text = upper is { } high ? $"{Number(lower, decimals)} ~ {Number(high, decimals)}" : Number(lower, decimals);
        return unit switch
        {
            IndicatorUnit.Percent => text + "%",
            IndicatorUnit.Index => text,
            _ => $"{text} {ReferenceDisplayNames.Get(unit).Get(ContentLanguage.Zh)}",
        };
    }

    /// <summary>锁定冲突中的值与变更历史写法相同：按字段对应的变更种类解读</summary>
    public static EventChangeKind KindOf(EventField field) => field switch
    {
        EventField.Actual => EventChangeKind.Actual,
        EventField.Forecast => EventChangeKind.Forecast,
        EventField.Previous => EventChangeKind.Previous,
        EventField.PreviousRevised => EventChangeKind.PreviousRevised,
        _ => EventChangeKind.Status,
    };

    public static string Number(decimal value, short decimals)
        => Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, CultureInfo.InvariantCulture);

    /// <summary>表单预填当前值时按指标小数位取整（库里是 numeric(18,6)，不然会显示成 29.000000）</summary>
    public static decimal? Round(decimal? value, short decimals)
        => value is { } number ? Math.Round(number, decimals, MidpointRounding.AwayFromZero) : null;

    /// <summary>数值输入框的 step：按指标小数位，浏览器据此即时提示小数位不对</summary>
    public static string Step(short decimals) => decimals == 0 ? "1" : "0." + new string('0', decimals - 1) + "1";

    /// <summary>数值输入框旁边显示的单位：与显示数值相同，百分比为 %，指数不显示</summary>
    public static string InputUnit(IndicatorUnit unit) => unit switch
    {
        IndicatorUnit.Percent => "%",
        IndicatorUnit.Index => "",
        _ => ReferenceDisplayNames.Get(unit).Get(ContentLanguage.Zh),
    };

    /// <summary>
    /// 变更历史中一个值（jsonb，写法见 EventChanges）的显示：改期带 UTC 时间（页面按操作者时区显示）和当地时间，
    /// 状态显示中文名，数值按指标小数位；认不出的原样显示
    /// </summary>
    public static (DateTime? At, string? Text) Change(EventChangeKind kind, string? json, short decimals)
    {
        if (json is null)
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            switch (kind)
            {
                case EventChangeKind.Rescheduled:
                    DateTime? at = root.TryGetProperty("at", out var a) && a.ValueKind == JsonValueKind.String
                        ? a.GetDateTime().ToUniversalTime()
                        : null;
                    var precision = (TimePrecision)root.GetProperty("precision").GetInt16();
                    var local = root.TryGetProperty("local", out var l) && l.GetString() is { } localText
                        ? DateTime.Parse(localText, CultureInfo.InvariantCulture)
                        : (DateTime?)null;
                    var localPart = local is { } value
                        ? precision == TimePrecision.Minute ? value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : "无日期";
                    return (precision == TimePrecision.Minute ? at : null, $"当地 {localPart}（{CalendarDisplayNames.Get(precision)}）");
                case EventChangeKind.Status:
                    return (null, CalendarDisplayNames.Get((CalendarEventStatus)root.GetInt16()));
                default:
                    return (null, root.ValueKind == JsonValueKind.String && decimal.TryParse(root.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                        ? Number(number, decimals)
                        : root.ToString());
            }
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            return (null, json);
        }
    }
}
