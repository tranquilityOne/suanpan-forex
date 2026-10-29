using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Reference.Rules;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Database;

/// <summary>
/// 迁移写入的基础数据：FR-005 验收第 1 条的 10 个经济体及其货币（基础数据设计 §2.4），以及首批指标（JAN-78）。
/// 预置数据手写在迁移中（HasData 会丢掉中英文文本），这里核对迁移实际写入的内容。
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class ReferencePresetDataTests(PostgresFixture postgres)
{
    private const string PresetActor = "migration";

    [Fact]
    public async Task Migration_PresetsTenEconomiesWithTheirCurrencies()
    {
        await using var db = await postgres.CreateAppContextAsync();

        var economies = await db.Economies.AsNoTracking()
            .Where(e => e.CreatedBy == PresetActor)
            .OrderBy(e => e.Sort)
            .Select(e => new { e.Code, e.CurrencyCode, e.TimeZone, e.Name.Zh, e.Name.En, e.IsEnabled })
            .ToListAsync(Ct);
        var currencies = await db.Currencies.AsNoTracking()
            .Where(c => c.CreatedBy == PresetActor)
            .Select(c => new { c.Code, c.Name.Zh, c.Name.En, c.IsEnabled })
            .ToListAsync(Ct);

        Assert.Equal(
            [
                ("US", "USD", "America/New_York"), ("EA", "EUR", "Europe/Brussels"), ("DE", "EUR", "Europe/Berlin"),
                ("GB", "GBP", "Europe/London"), ("JP", "JPY", "Asia/Tokyo"), ("CN", "CNY", "Asia/Shanghai"),
                ("CH", "CHF", "Europe/Zurich"), ("CA", "CAD", "America/Toronto"), ("AU", "AUD", "Australia/Sydney"),
                ("NZ", "NZD", "Pacific/Auckland"),
            ],
            economies.Select(e => (e.Code, e.CurrencyCode, e.TimeZone)));
        Assert.Equal(
            ["AUD", "CAD", "CHF", "CNY", "EUR", "GBP", "JPY", "NZD", "USD"],
            currencies.Select(c => c.Code).Order(StringComparer.Ordinal));

        Assert.All(economies, e => Assert.True(e.IsEnabled && e.Zh.Length > 0 && e.En.Length > 0 && TimeZoneRule.IsValid(e.TimeZone)));
        Assert.All(currencies, c => Assert.True(c.IsEnabled && c.Zh.Length > 0 && c.En.Length > 0));
    }

    /// <summary>
    /// 首批指标（JAN-78，docs/design/reference-indicators.md）：迁移导入 50 个高重要性指标，都符合编码规则和指标规则，
    /// 经济体和货币是预置的，前台地址不重复。数据是否正确由编辑在后台逐条复核（FR-006 验收第 1 条）。
    /// 其中 7 个非政府机构的指标暂不纳入日历，由迁移停用（首批指标清单 §2 第 9 项，JAN-87）
    /// </summary>
    [Fact]
    public async Task Migration_ImportsInitialIndicatorsThatPassTheRules()
    {
        await using var db = await postgres.CreateAppContextAsync();

        var indicators = await db.Indicators.AsNoTracking().Where(i => i.CreatedBy == PresetActor).ToListAsync(Ct);
        var economies = await db.Economies.AsNoTracking().Where(e => e.CreatedBy == PresetActor).Select(e => e.Code).ToListAsync(Ct);
        var currencies = await db.Currencies.AsNoTracking().Where(c => c.CreatedBy == PresetActor).Select(c => c.Code).ToListAsync(Ct);

        Assert.Equal(50, indicators.Count);
        Assert.All(indicators, i =>
        {
            Assert.Null(ReferenceCodeRule.ValidateIndicatorCode(i.Code, i.EconomyCode));
            Assert.Null(IndicatorRule.Validate(i));
            Assert.Contains(i.EconomyCode, economies);
            Assert.Contains(i.CurrencyCode, currencies);
            Assert.Equal(Importance.High, i.Importance);
        });
        Assert.Equal(
            ["DE.IFO", "DE.ZEW", "EA.PMI.MFG.PRELIM", "EA.PMI.SVC.PRELIM", "US.ISM.MFG", "US.ISM.SVC", "US.UMCSI.PRELIM"],
            indicators.Where(i => !i.IsEnabled).Select(i => i.Code).Order(StringComparer.Ordinal));
        Assert.Equal(indicators.Count, indicators.Select(i => ReferenceCodeRule.ToSlug(i.Code)).Distinct().Count());

        // 抽查清单中特殊的几项：区间型的利率决议、单位"千个"、德国用欧元
        var byCode = indicators.ToDictionary(i => i.Code);
        Assert.Equal((IndicatorValueType.Range, (short)2, ReleaseFrequency.Irregular, new TimeOnly(14, 0)),
            (byCode["US.RATE"].ValueType, byCode["US.RATE"].Decimals, byCode["US.RATE"].Frequency, byCode["US.RATE"].DefaultReleaseTime));
        Assert.Equal(IndicatorUnit.ThousandUnits, byCode["US.JOLTS"].Unit);
        Assert.Equal("EUR", byCode["DE.CPI.MOM.PRELIM"].CurrencyCode);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
