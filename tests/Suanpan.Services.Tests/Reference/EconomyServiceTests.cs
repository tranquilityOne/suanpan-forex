using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Data;
using Suanpan.Services.Reference.Currencies;
using Suanpan.Services.Reference.Currencies.Models;
using Suanpan.Services.Reference.Economies;
using Suanpan.Services.Reference.Economies.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Reference;

/// <summary>经济体的保存、停用和删除（基础数据设计 §3.3、§3.5，FR-005 验收第 2、3 条）</summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class EconomyServiceTests(PostgresFixture postgres)
{
    private const string NewYork = "America/New_York";

    [Fact]
    public async Task Create_NormalizesCodesAndSavesEnabled()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new EconomyService(db);

        var result = await service.CreateAsync(new EconomyInput(" qb ", "测试国", "Testland", "usd", NewYork, 900), Ct);

        Assert.True(result.Success, result.Error);
        var saved = await service.GetAsync("QB", Ct);
        Assert.NotNull(saved);
        Assert.Equal(("USD", NewYork, (short)900, true), (saved.CurrencyCode, saved.TimeZone, saved.Sort, saved.IsEnabled));
    }

    /// <summary>
    /// 编码与预置的 US 重复或格式不对（ERR-REF-001）、货币不存在、时区不是 IANA 名称（ERR-REF-003，Windows 名称也不行），
    /// 都在对应的字段提示
    /// </summary>
    [Theory]
    [InlineData("us", "USD", NewYork, nameof(EconomyInput.Code))]
    [InlineData("Q1", "USD", NewYork, nameof(EconomyInput.Code))]
    [InlineData("QC", "XZZ", NewYork, nameof(EconomyInput.CurrencyCode))]
    [InlineData("QC", "USD", "Eastern Standard Time", nameof(EconomyInput.TimeZone))]
    [InlineData("QC", "USD", "Mars/Olympus", nameof(EconomyInput.TimeZone))]
    public async Task Create_RejectsInvalidInput(string code, string currencyCode, string timeZone, string field)
    {
        await using var db = await postgres.CreateAppContextAsync();

        var result = await new EconomyService(db).CreateAsync(new EconomyInput(code, "测试国", "Testland", currencyCode, timeZone, 900), Ct);

        Assert.False(result.Success);
        Assert.Equal(field, result.Field);
    }

    /// <summary>BR-028：停用的货币不能被新选用</summary>
    [Fact]
    public async Task Create_RejectsDisabledCurrency()
    {
        await using var db = await postgres.CreateAppContextAsync();
        await AddCurrencyAsync(db, "XBA", enabled: false);

        var result = await new EconomyService(db).CreateAsync(new EconomyInput("QD", "测试国", "Testland", "XBA", NewYork, 900), Ct);

        Assert.False(result.Success);
        Assert.Equal(nameof(EconomyInput.CurrencyCode), result.Field);
    }

    /// <summary>货币停用后，已在使用它的经济体照常保存；换成另一个停用的货币则不行</summary>
    [Fact]
    public async Task Update_KeepsCurrentDisabledCurrencyButRejectsSwitchingToOne()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new EconomyService(db);
        await AddCurrencyAsync(db, "XBB", enabled: true);
        await AddCurrencyAsync(db, "XBC", enabled: false);
        await service.CreateAsync(new EconomyInput("QE", "测试国", "Testland", "XBB", NewYork, 900), Ct);
        await new CurrencyService(db).SetEnabledAsync("XBB", false, Ct);

        var kept = await service.UpdateAsync(new EconomyInput("QE", "测试国二", "Testland II", "XBB", "Europe/London", 910), Ct);
        var switched = await service.UpdateAsync(new EconomyInput("QE", "测试国二", "Testland II", "XBC", "Europe/London", 910), Ct);

        Assert.True(kept.Success, kept.Error);
        Assert.False(switched.Success);
        Assert.Equal(nameof(EconomyInput.CurrencyCode), switched.Field);
        var saved = await service.GetAsync("QE", Ct);
        Assert.Equal(("测试国二", "XBB", "Europe/London", (short)910), (saved!.Name.Zh, saved.CurrencyCode, saved.TimeZone, saved.Sort));
    }

    [Fact]
    public async Task SetEnabled_DisabledEconomyIsNotOffered()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new EconomyService(db);
        await service.CreateAsync(new EconomyInput("QF", "测试国", "Testland", "USD", NewYork, 900), Ct);

        var result = await service.SetEnabledAsync("QF", false, Ct);

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain(await service.GetOptionsAsync(Ct), e => e.Code == "QF");
    }

    /// <summary>FR-005 验收第 3 条、ERR-REF-002：有指标的经济体不能删除，提示中列出指标</summary>
    [Fact]
    public async Task Delete_RejectsEconomyWithIndicators()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new EconomyService(db);
        await service.CreateAsync(new EconomyInput("QH", "测试国", "Testland", "USD", NewYork, 900), Ct);
        var indicator = TestData.IndicatorCode("QH");
        await new IndicatorService(db).CreateAsync(TestData.Indicator(indicator, "QH"), Ct);

        var result = await service.DeleteAsync("QH", Ct);

        Assert.False(result.Success);
        Assert.Contains(indicator, result.Error);
        Assert.NotNull(await service.GetAsync("QH", Ct));
    }

    /// <summary>经济体被数据源引用时同样不能删除，提示中列出数据源（采集设计 §3.1）</summary>
    [Fact]
    public async Task Delete_RejectsEconomyWithDataSources()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new EconomyService(db);
        await service.CreateAsync(new EconomyInput("QL", "测试国", "Testland", "USD", NewYork, 900), Ct);
        var source = TestData.Unique("源");
        db.DataSources.Add(new DataSource
        {
            Name = source, Agency = "测试机构", EconomyCode = "QL", Type = DataSourceType.OfficialWeb, BaseUrl = "https://example.org",
            RateLimit = 1, RateLimitUnit = RateLimitUnit.PerSecond, ComplianceClass = ComplianceClass.GovernmentPublic,
            TermsUrl = "https://example.org/terms", RobotsResult = RobotsResult.Allowed,
        });
        await db.SaveChangesAsync(Ct);

        var result = await service.DeleteAsync("QL", Ct);

        Assert.False(result.Success);
        Assert.Contains(source, result.Error);
    }

    [Fact]
    public async Task Delete_RemovesUnreferencedEconomy()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new EconomyService(db);
        await service.CreateAsync(new EconomyInput("QG", "测试国", "Testland", "USD", NewYork, 900), Ct);

        var result = await service.DeleteAsync("QG", Ct);

        Assert.True(result.Success, result.Error);
        Assert.Null(await service.GetAsync("QG", Ct));
    }

    private static async Task AddCurrencyAsync(SuanpanDbContext db, string code, bool enabled)
    {
        var currencies = new CurrencyService(db);
        await currencies.CreateAsync(new CurrencyInput(code, "测试币", "Test Dollar"), Ct);
        if (!enabled)
        {
            await currencies.SetEnabledAsync(code, false, Ct);
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
