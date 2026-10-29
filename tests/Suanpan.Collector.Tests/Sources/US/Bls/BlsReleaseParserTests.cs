using Suanpan.Collector.Execution;
using Suanpan.Collector.Sources.US.Bls;
using Suanpan.Core.Calendar.Records;
using Suanpan.Core.Reference.Enums;
using Xunit;

namespace Suanpan.Collector.Tests.Sources.US.Bls;

/// <summary>
/// BLS API v2 公布值的解析（日历设计 §5.1，BR-043，NFR-133）：按外部标识的计算方式从水平值算出每一期，
/// 单位按序列判断，原始字符串记参与计算的序列和数值。样例中各序列的最新一期为 2026-09（JOLTS 为 2026-08）
/// </summary>
public class BlsReleaseParserTests
{
    private readonly BlsReleaseParser _parser = new();

    /// <summary>四种计算方式各取一个首批指标，值未舍入（入库时按指标小数位舍入）</summary>
    [Fact]
    public void Parse_CalculatesEachMethodFromRealResponse()
    {
        var result = Parse("CES0000000001:chg", "LNS14000000", "CUSR0000SA0:pct", "CUUR0000SA0:pct12", "JTS000000000000000JOL");

        Assert.Empty(result.Missing);
        Assert.Equal((29m, IndicatorUnit.ThousandPersons, "CES0000000001 2026-09=159044 2026-08=159015"), Latest(result, "CES0000000001:chg"));
        Assert.Equal((4.2m, IndicatorUnit.Percent, "LNS14000000 2026-09=4.2"), Latest(result, "LNS14000000"));
        Assert.Equal((0.649m, IndicatorUnit.Percent, "CUSR0000SA0 2026-09=336.298 2026-08=334.131"), Latest(result, "CUSR0000SA0:pct"));
        Assert.Equal((3.738m, IndicatorUnit.Percent, "CUUR0000SA0 2026-09=336.940 2025-09=324.800"), Latest(result, "CUUR0000SA0:pct12"));
        Assert.Equal((7079m, IndicatorUnit.ThousandUnits, "JTS000000000000000JOL 2026-08=7079"), Latest(result, "JTS000000000000000JOL"));
        Assert.All(Records(result), r => Assert.Equal(0, r.ResponseIndex));
    }

    /// <summary>2025-10 的值为 "-"：这一期没有值，依赖它的下一期月率也不输出；其他月份照常</summary>
    [Fact]
    public void Parse_SkipsMonthsWithoutValue()
    {
        var periods = Records(Parse("CUSR0000SA0:pct")).Select(r => r.ReferencePeriod).ToList();

        Assert.DoesNotContain("2025-10", periods);
        Assert.DoesNotContain("2025-11", periods);
        Assert.Contains("2025-12", periods);
    }

    /// <summary>响应中没有的序列、没有登记单位的序列（平均时薪的水平值是美元）记为缺失项，运行部分成功（ERR-PARSE-002）</summary>
    [Fact]
    public void Parse_ReportsMissingSeriesAndUnknownUnits()
    {
        var result = Parse("LNS14000000", "LNS11000000", "CES0500000003", "CES0500000003:pct");

        Assert.Equal(2, result.Missing.Count);
        Assert.Equal(["CES0500000003:pct", "LNS14000000"], Records(result).Select(r => r.ExternalId).Distinct().Order());
    }

    /// <summary>API 没有处理请求（如 Key 无效、超过每日限额）时快照无效（ERR-PARSE-003），提示信息写进原因</summary>
    [Fact]
    public void Parse_RequestNotProcessed_IsInvalidSnapshot()
    {
        var response = BlsSnapshots.Inline("https://api.bls.gov/publicAPI/v2/timeseries/data/",
            """{"status":"REQUEST_NOT_PROCESSED","responseTime":10,"message":["daily threshold for total number of requests allocated to the user has been reached."],"Results":{}}""",
            "application/json");

        var ex = Assert.Throws<InvalidSnapshotException>(() => _parser.Parse(new FetchResult([response]), """{"series": ["LNS14000000"]}"""));
        Assert.Contains("daily threshold", ex.Message);
    }

    private ParseResult Parse(params string[] externalIds)
        => _parser.Parse(new FetchResult([BlsSnapshots.ApiResponse()]), $$"""{"series": [{{string.Join(", ", externalIds.Select(id => $"\"{id}\""))}}]}""");

    private static List<ReleaseValueRecord> Records(ParseResult result) => result.Records.Cast<ReleaseValueRecord>().ToList();

    /// <summary>数据中最新一期的值（保留 3 位小数比较）、单位、原始字符串</summary>
    private static (decimal, IndicatorUnit, string) Latest(ParseResult result, string externalId)
    {
        var latest = Records(result).Where(r => r.ExternalId == externalId).MaxBy(r => r.ReferencePeriod)!;
        return (Math.Round(latest.Value, 3), latest.Unit, latest.Raw);
    }
}
