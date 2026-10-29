using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Suanpan.Collector.Execution;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Records;

namespace Suanpan.Collector.Sources.US.Bls;

/// <summary>
/// BLS 发布日程的解析器（日历设计 §4.1）：每个响应是一种发布的日程页，表格 <c>release-list</c> 的每一行
/// （Reference Month、Release Date、Release Time，如 October 2026、Nov. 06, 2026、08:30 AM）输出一条 <see cref="ScheduleEntryRecord"/>。
/// 发布标识取页面的文件名，发布名称取页面标题；没有发布时间的行为"仅日期"。
/// 参考月份、日期、时间写法不认识的行照样输出（对应字段为空），由排期入库计为失败（ERR-PARSE-005）
/// </summary>
public sealed partial class BlsScheduleParser : IParser
{
    public const string ParserKey = BlsWebAdapter.AdapterKey + ".schedule";

    private static readonly string[] ExpectedHeaders = ["Reference Month", "Release Date", "Release Time"];

    private static readonly string[] MonthAbbreviations = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    private static readonly string[] TimeFormats = ["hh:mm tt", "h:mm tt"];

    public string Key => ParserKey;

    public ParseResult Parse(FetchResult fetched, string? parameters)
    {
        var records = new List<object>();
        for (var index = 0; index < fetched.Responses.Count; index++)
        {
            var response = fetched.Responses[index];
            var html = response.Text();
            var page = response.Url.AbsolutePath;
            var table = ReleaseTable().Match(html);
            if (!table.Success)
            {
                throw html.Contains("Access Denied", StringComparison.OrdinalIgnoreCase)
                    ? new InvalidSnapshotException($"{page} 是拦截页（Access Denied）")
                    : new ParseException($"{page} 中没有发布日程表（release-list），页面可能已改版");
            }

            var headers = HeaderCell().Matches(table.Value).Select(m => CellText(m.Groups[1].Value)).ToList();
            if (!headers.SequenceEqual(ExpectedHeaders))
            {
                throw new ParseException($"{page} 的日程表列为 {string.Join(" / ", headers)}，与预期的 {string.Join(" / ", ExpectedHeaders)} 不符");
            }

            var releaseKey = BlsWebAdapter.ReleaseKeyOf(response.Url);
            var releaseName = Title().Match(html) is { Success: true } title ? CellText(title.Groups["name"].Value) : releaseKey;
            foreach (Match row in Row().Matches(table.Value))
            {
                var cells = DataCell().Matches(row.Groups[1].Value).Select(m => CellText(m.Groups[1].Value)).ToList();
                if (cells.Count > 0)
                {
                    records.Add(Entry(releaseKey, releaseName, cells, index));
                }
            }
        }

        return new ParseResult(records, []);
    }

    private static ScheduleEntryRecord Entry(string releaseKey, string releaseName, List<string> cells, int index)
    {
        var period = DateTime.TryParseExact(cells[0], "MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
            ? month.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : null;
        var date = cells.Count > 1 ? ReleaseDate(cells[1]) : null;
        var timeText = cells.Count > 2 ? cells[2] : string.Empty;
        if (timeText.Length == 0)
        {
            return new ScheduleEntryRecord(releaseKey, releaseName, period, date, null, TimePrecision.DateOnly, index);
        }

        TimeOnly? time = TimeOnly.TryParseExact(timeText, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
        return new ScheduleEntryRecord(releaseKey, releaseName, period, date, time, TimePrecision.Minute, index);
    }

    /// <summary>发布日期：月份缩写（可带句点，Sept 也认）、日、年，如 Nov. 06, 2026、May 07, 2027</summary>
    private static DateOnly? ReleaseDate(string text)
    {
        var match = ReleaseDateFormat().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var month = Array.IndexOf(MonthAbbreviations, match.Groups["month"].Value[..3].ToLowerInvariant()) + 1;
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        return month > 0 && day <= DateTime.DaysInMonth(year, month) ? new DateOnly(year, month, day) : null;
    }

    /// <summary>单元格的文字：去掉标签、解码实体、合并空白</summary>
    private static string CellText(string html)
        => Whitespace().Replace(WebUtility.HtmlDecode(Tag().Replace(html, " ")), " ").Trim();

    [GeneratedRegex(@"<table[^>]*class=""release-list""[^>]*>.*?</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ReleaseTable();

    [GeneratedRegex(@"<th[^>]*>(.*?)</th>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeaderCell();

    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Row();

    [GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex DataCell();

    [GeneratedRegex(@"<title>\s*Schedule of Releases for (?:the )?(?<name>[^<]+?)\s*</title>", RegexOptions.IgnoreCase)]
    private static partial Regex Title();

    [GeneratedRegex(@"^(?<month>[A-Za-z]{3,4})\.?\s+(?<day>\d{1,2}),\s*(?<year>\d{4})\z")]
    private static partial Regex ReleaseDateFormat();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
