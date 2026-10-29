using Suanpan.Collector.Execution;
using Suanpan.Collector.Sources.US.Bls;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Records;
using Xunit;

namespace Suanpan.Collector.Tests.Sources.US.Bls;

/// <summary>BLS 发布日程的解析（日历设计 §4.1，NFR-133）：发布标识取页面文件名，发布名称取标题，每行一条日程条目</summary>
public class BlsScheduleParserTests
{
    private readonly BlsScheduleParser _parser = new();

    /// <summary>四个真实日程页：每一行的参考月份、日期、时间都认得出，记录对应到所在的响应</summary>
    [Fact]
    public void Parse_RealSchedulePages()
    {
        string[] releases = ["empsit", "cpi", "ppi", "jolts"];

        var records = Parse(releases.Select(BlsSnapshots.SchedulePage).ToArray());

        Assert.Equal([("empsit", 0, 14), ("cpi", 1, 14), ("ppi", 2, 14), ("jolts", 3, 1)],
            records.GroupBy(r => (r.ReleaseKey, r.ResponseIndex)).Select(g => (g.Key.ReleaseKey, g.Key.ResponseIndex, g.Count())));
        Assert.All(records, r => Assert.True(r is { ReferencePeriod: not null, LocalDate: not null, LocalTime: not null, Precision: TimePrecision.Minute }));
        Assert.Equal(
            new ScheduleEntryRecord("empsit", "Employment Situation", "2026-10", new DateOnly(2026, 11, 6), new TimeOnly(8, 30), TimePrecision.Minute, 0),
            records[0]);
        Assert.Equal(
            new ScheduleEntryRecord("jolts", "Job Openings and Labor Turnover Survey", "2026-10", new DateOnly(2026, 12, 1), new TimeOnly(10, 0), TimePrecision.Minute, 3),
            records[^1]);
    }

    /// <summary>没有发布时间的行为"仅日期"；日期写法不认识时日期为空，由入库计为失败（ERR-PARSE-005）</summary>
    [Fact]
    public void Parse_RowsWithoutTimeOrUnknownDate()
    {
        var page = Page("""
            <tr><td>December 2026</td><td>Sept. 08, 2027</td></tr>
            <tr><td>January 2027</td><td>To be announced</td><td></td></tr>
            """);

        var records = Parse(page);

        Assert.Equal(
            [("2026-12", (DateOnly?)new DateOnly(2027, 9, 8), TimePrecision.DateOnly), ("2027-01", null, TimePrecision.DateOnly)],
            records.Select(r => (r.ReferencePeriod, r.LocalDate, r.Precision)));
    }

    /// <summary>拦截页是无效快照（ERR-PARSE-003）；页面改版、表格的列变了是解析失败（ERR-PARSE-001），改好解析器后可以重新解析</summary>
    [Fact]
    public void Parse_BlockedOrChangedPage_Throws()
    {
        var blocked = BlsSnapshots.Inline("https://www.bls.gov/schedule/news_release/cpi.htm", "<html><title>Access Denied</title></html>");
        var changed = Page("<tr><td>October 2026</td></tr>", headers: "<th>Month</th><th>Date</th>");

        Assert.Throws<InvalidSnapshotException>(() => Parse(blocked));
        var ex = Assert.Throws<ParseException>(() => Parse(changed));
        Assert.IsNotType<InvalidSnapshotException>(ex);
    }

    private List<ScheduleEntryRecord> Parse(params FetchedResponse[] responses)
        => _parser.Parse(new FetchResult(responses), """{"releases": ["empsit"]}""").Records.Cast<ScheduleEntryRecord>().ToList();

    private static FetchedResponse Page(string rows, string headers = "<th>Reference Month</th><th>Release Date</th><th>Release Time</th>")
        => BlsSnapshots.Inline("https://www.bls.gov/schedule/news_release/empsit.htm",
            $"""<html><title>Schedule of Releases for the Employment Situation</title><table class="release-list"><thead><tr>{headers}</tr></thead><tbody>{rows}</tbody></table></html>""");
}
