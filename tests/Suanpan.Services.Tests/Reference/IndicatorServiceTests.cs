using Suanpan.Core.Reference.Enums;
using Suanpan.Services.Reference.Currencies;
using Suanpan.Services.Reference.Currencies.Models;
using Suanpan.Services.Reference.Economies;
using Suanpan.Services.Reference.Economies.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Reference.Indicators.Models;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Reference;

/// <summary>
/// 指标的保存、停用和删除（基础数据设计 §3.4、§3.5，FR-006）。字段规则的各个分支在 Core 的 IndicatorRuleTests 中测试，
/// 这里只测服务接上了规则，以及需要查库的校验
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class IndicatorServiceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Create_NormalizesCodesAndSavesEnabled()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IndicatorService(db);
        var code = TestData.IndicatorCode();

        var result = await service.CreateAsync(TestData.Indicator($" {code.ToLowerInvariant()} ", "us", "usd") with { DescriptionZh = " ", DescriptionEn = null }, Ct);

        Assert.True(result.Success, result.Error);
        var saved = await service.GetAsync(code, Ct);
        Assert.NotNull(saved);
        Assert.Equal(("US", "USD", true), (saved.EconomyCode, saved.CurrencyCode, saved.IsEnabled));
        Assert.Null(saved.Description);
    }

    /// <summary>经济体不存在、编码不以经济体开头、货币不存在、字段规则不通过，都在对应的字段提示</summary>
    [Theory]
    [InlineData("QZ", "QZ.TEST", "USD", nameof(IndicatorInput.EconomyCode))]
    [InlineData("US", "EA.TEST", "USD", nameof(IndicatorInput.Code))]
    [InlineData("US", "US.TEST", "XZZ", nameof(IndicatorInput.CurrencyCode))]
    public async Task Create_RejectsInvalidReferences(string economyCode, string code, string currencyCode, string field)
    {
        await using var db = await postgres.CreateAppContextAsync();

        var result = await new IndicatorService(db).CreateAsync(TestData.Indicator(code, economyCode, currencyCode), Ct);

        Assert.False(result.Success);
        Assert.Equal(field, result.Field);
    }

    [Fact]
    public async Task Create_AppliesIndicatorRule()
    {
        await using var db = await postgres.CreateAppContextAsync();

        var result = await new IndicatorService(db).CreateAsync(TestData.Indicator(TestData.IndicatorCode()) with { SigmaN = 0 }, Ct);

        Assert.False(result.Success);
        Assert.Equal(nameof(IndicatorInput.SigmaN), result.Field);
    }

    /// <summary>ERR-IND-001</summary>
    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IndicatorService(db);
        var code = TestData.IndicatorCode();
        await service.CreateAsync(TestData.Indicator(code), Ct);

        var result = await service.CreateAsync(TestData.Indicator(code), Ct);

        Assert.False(result.Success);
        Assert.Equal(nameof(IndicatorInput.Code), result.Field);
    }

    /// <summary>BR-028：停用的经济体、货币不能被新选用</summary>
    [Fact]
    public async Task Create_RejectsDisabledEconomyOrCurrency()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var currencies = new CurrencyService(db);
        await currencies.CreateAsync(new CurrencyInput("XCA", "测试币", "Test Dollar"), Ct);
        await currencies.SetEnabledAsync("XCA", false, Ct);
        var economies = new EconomyService(db);
        await economies.CreateAsync(new EconomyInput("QI", "测试国", "Testland", "USD", "America/New_York", 900), Ct);
        await economies.SetEnabledAsync("QI", false, Ct);
        var service = new IndicatorService(db);

        var disabledCurrency = await service.CreateAsync(TestData.Indicator(TestData.IndicatorCode(), currencyCode: "XCA"), Ct);
        var disabledEconomy = await service.CreateAsync(TestData.Indicator(TestData.IndicatorCode("QI"), "QI"), Ct);

        Assert.Equal((false, nameof(IndicatorInput.CurrencyCode)), (disabledCurrency.Success, disabledCurrency.Field));
        Assert.Equal((false, nameof(IndicatorInput.EconomyCode)), (disabledEconomy.Success, disabledEconomy.Field));
    }

    /// <summary>BR-029：编码和所属经济体创建后不可修改，修改时输入中的经济体被忽略</summary>
    [Fact]
    public async Task Update_KeepsCodeAndEconomy()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IndicatorService(db);
        var code = TestData.IndicatorCode();
        await service.CreateAsync(TestData.Indicator(code), Ct);

        var result = await service.UpdateAsync(TestData.Indicator(code, economyCode: "EA", currencyCode: "EUR") with { NameZh = "改名后" }, Ct);

        Assert.True(result.Success, result.Error);
        var saved = await service.GetAsync(code, Ct);
        Assert.Equal(("US", "EUR", "改名后"), (saved!.EconomyCode, saved.CurrencyCode, saved.Name.Zh));
    }

    /// <summary>BR-031、ERR-IND-002：影响方向改变时必须填写理由；没填时不保存</summary>
    [Fact]
    public async Task Update_DirectionChangeRequiresReason()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IndicatorService(db);
        var code = TestData.IndicatorCode();
        await service.CreateAsync(TestData.Indicator(code), Ct);
        var changed = TestData.Indicator(code) with { Direction = ImpactDirection.HigherIsBearish };

        var withoutReason = await service.UpdateAsync(changed, Ct);
        var unchanged = await service.GetAsync(code, Ct);
        var withReason = await service.UpdateAsync(changed with { Reason = "统计口径调整" }, Ct);

        Assert.False(withoutReason.Success);
        Assert.Equal(nameof(IndicatorInput.Reason), withoutReason.Field);
        Assert.Equal(ImpactDirection.HigherIsBullish, unchanged!.Direction);
        Assert.True(withReason.Success, withReason.Error);
        Assert.Equal(ImpactDirection.HigherIsBearish, (await service.GetAsync(code, Ct))!.Direction);
    }

    [Fact]
    public async Task Query_FiltersByKeywordAndImportance()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IndicatorService(db);
        var high = TestData.IndicatorCode();
        var low = high[..^1] + (high[^1] == 'A' ? 'B' : 'A');
        await service.CreateAsync(TestData.Indicator(high) with { Importance = Importance.High }, Ct);
        await service.CreateAsync(TestData.Indicator(low) with { Importance = Importance.Low }, Ct);

        var byKeyword = await service.QueryAsync(new IndicatorQuery(Keyword: high[..^1].ToLowerInvariant()), Ct);
        var byImportance = await service.QueryAsync(new IndicatorQuery(Keyword: high[..^1], Importance: Importance.High), Ct);

        Assert.Equal(new[] { high, low }.Order(), byKeyword.Items.Select(i => i.Code).Order());
        var row = Assert.Single(byImportance.Items);
        Assert.Equal((high, "美国"), (row.Code, row.EconomyName.Zh));
    }

    /// <summary>BR-153：没有被引用的指标可以删除（被引用时不能删除的情况随数据源映射、日历等模块一起实现）</summary>
    [Fact]
    public async Task Delete_RemovesUnreferencedIndicator()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IndicatorService(db);
        var code = TestData.IndicatorCode();
        await service.CreateAsync(TestData.Indicator(code), Ct);

        var result = await service.DeleteAsync(code, Ct);

        Assert.True(result.Success, result.Error);
        Assert.Null(await service.GetAsync(code, Ct));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
