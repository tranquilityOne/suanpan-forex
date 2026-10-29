using System.Globalization;
using System.Text.Json;
using Suanpan.Collector.Execution;
using Suanpan.Core.Calendar.Records;

namespace Suanpan.Collector.Sources.US.Bls;

/// <summary>
/// BLS API v2 公布值的解析器（日历设计 §5.1）：按任务参数中的外部标识，从各序列的月度数据算出每一期的值，输出 <see cref="ReleaseValueRecord"/>。
/// 只取月度数据（M01 ~ M12，不取年均值 M13）；值为 "-" 的月份（如 2025 年停摆期间）没有值，依赖它的变动、变动率也不输出。
/// 序列没有数据、单位没有登记时记为缺失项（运行部分成功）；API 没有处理请求（如 Key 无效、超过每日限额）时快照无效
/// </summary>
public sealed class BlsReleaseParser : IParser
{
    public const string ParserKey = BlsApiAdapter.AdapterKey + ".release";

    private const string Succeeded = "REQUEST_SUCCEEDED";

    public string Key => ParserKey;

    public ParseResult Parse(FetchResult fetched, string? parameters)
    {
        var specs = BlsParameters.Series(parameters);
        var series = ReadSeries(fetched);
        var records = new List<object>();
        var missing = new List<string>();
        foreach (var spec in specs)
        {
            if (!series.TryGetValue(spec.SeriesId, out var data) || data.Months.Count == 0)
            {
                missing.Add($"{spec.ExternalId}：BLS 没有返回这个序列的月度数据");
                continue;
            }

            if (spec.Unit is not { } unit)
            {
                missing.Add($"{spec.ExternalId}：没有登记序列 {spec.SeriesId} 的单位");
                continue;
            }

            foreach (var period in data.Months.Keys)
            {
                if (Calculate(spec, period, data.Months) is { } result)
                {
                    records.Add(new ReleaseValueRecord(spec.ExternalId, period, result.Value, null, unit, result.Raw, data.ResponseIndex));
                }
            }
        }

        return new ParseResult(records, missing);
    }

    /// <summary>按计算方式算出一期的值；参与计算的月份没有值时返回 null。原始字符串记参与计算的序列和数值（BR-043）</summary>
    private static (decimal Value, string Raw)? Calculate(BlsSeriesSpec spec, string period, SortedDictionary<string, string> months)
    {
        var current = months[period];
        var value = decimal.Parse(current, CultureInfo.InvariantCulture);
        if (spec.Calculation == BlsCalculation.Level)
        {
            return (value, $"{spec.SeriesId} {period}={current}");
        }

        var basePeriod = ShiftMonths(period, spec.Calculation == BlsCalculation.PercentChange12 ? -12 : -1);
        if (!months.TryGetValue(basePeriod, out var previous))
        {
            return null;
        }

        var baseValue = decimal.Parse(previous, CultureInfo.InvariantCulture);
        var raw = $"{spec.SeriesId} {period}={current} {basePeriod}={previous}";
        return spec.Calculation switch
        {
            BlsCalculation.Change => (value - baseValue, raw),
            _ when baseValue == 0 => null,
            _ => ((value / baseValue - 1) * 100, raw),
        };
    }

    /// <summary>读出各响应中的序列：序列 ID → 来自第几个响应、各月的值（参考期 YYYY-MM → 原始字符串）</summary>
    private static Dictionary<string, (int ResponseIndex, SortedDictionary<string, string> Months)> ReadSeries(FetchResult fetched)
    {
        var series = new Dictionary<string, (int, SortedDictionary<string, string>)>(StringComparer.Ordinal);
        for (var index = 0; index < fetched.Responses.Count; index++)
        {
            using var document = ReadJson(fetched.Responses[index]);
            var root = document.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (status != Succeeded)
            {
                throw new InvalidSnapshotException($"BLS API 没有处理请求（{status}）：{Messages(root)}");
            }

            if (!root.TryGetProperty("Results", out var results) || !results.TryGetProperty("series", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                throw new ParseException("BLS API 的响应中没有 Results.series");
            }

            foreach (var item in list.EnumerateArray())
            {
                var months = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var point in item.GetProperty("data").EnumerateArray())
                {
                    var year = point.GetProperty("year").GetString();
                    var periodCode = point.GetProperty("period").GetString();
                    var value = point.GetProperty("value").GetString();
                    if (MonthlyPeriod(year, periodCode) is { } period
                        && decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
                    {
                        months[period] = value!;
                    }
                }

                series[item.GetProperty("seriesID").GetString()!] = (index, months);
            }
        }

        return series;
    }

    private static JsonDocument ReadJson(FetchedResponse response)
    {
        try
        {
            return JsonDocument.Parse(response.Body);
        }
        catch (JsonException)
        {
            throw response.Text().Contains("Access Denied", StringComparison.OrdinalIgnoreCase)
                ? new InvalidSnapshotException("BLS API 返回的是拦截页（Access Denied）")
                : new ParseException("BLS API 的响应不是 JSON");
        }
    }

    /// <summary>API 的提示信息，最多 500 字</summary>
    private static string Messages(JsonElement root)
    {
        var text = root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Array
            ? string.Join("；", message.EnumerateArray().Select(m => m.GetString()))
            : "没有提示信息";
        return text.Length > 500 ? text[..500] : text;
    }

    /// <summary>月度数据的参考期：年份 + M01 ~ M12 → YYYY-MM；年均值 M13 和其他频率返回 null</summary>
    private static string? MonthlyPeriod(string? year, string? period)
        => year is { Length: 4 } && period is { Length: 3 } && period[0] == 'M'
           && int.TryParse(period[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var month) && month is >= 1 and <= 12
            ? $"{year}-{month:00}"
            : null;

    private static string ShiftMonths(string period, int months)
        => DateOnly.ParseExact(period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).AddMonths(months).ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
