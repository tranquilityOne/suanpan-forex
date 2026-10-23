using Suanpan.Services.Reference.Currencies;
using Suanpan.Services.Reference.Currencies.Models;
using Suanpan.Services.Reference.Economies;
using Suanpan.Services.Reference.Economies.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Reference;

/// <summary>货币的保存、停用和删除（基础数据设计 §3.3、§3.5，FR-005 验收第 2、3 条）</summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CurrencyServiceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Create_NormalizesCodeAndSavesEnabled()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new CurrencyService(db);

        var result = await service.CreateAsync(new CurrencyInput(" xaa ", "测试币", "Test Dollar"), Ct);

        Assert.True(result.Success, result.Error);
        var saved = await service.GetAsync("XAA", Ct);
        Assert.NotNull(saved);
        Assert.True(saved.IsEnabled);
        Assert.Equal(("测试币", "Test Dollar"), (saved.Name.Zh, saved.Name.En));
    }

    /// <summary>ERR-REF-001：与预置的 USD 重复（大小写不同也算），或格式不对，都在编码字段提示</summary>
    [Theory]
    [InlineData("usd")]
    [InlineData("U1D")]
    public async Task Create_RejectsDuplicateOrMalformedCode(string code)
    {
        await using var db = await postgres.CreateAppContextAsync();

        var result = await new CurrencyService(db).CreateAsync(new CurrencyInput(code, "测试币", "Test Dollar"), Ct);

        Assert.False(result.Success);
        Assert.Equal(nameof(CurrencyInput.Code), result.Field);
    }

    [Fact]
    public async Task SetEnabled_DisabledCurrencyIsNotOffered()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new CurrencyService(db);
        await service.CreateAsync(new CurrencyInput("XAB", "测试币", "Test Dollar"), Ct);

        var result = await service.SetEnabledAsync("XAB", false, Ct);

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain(await service.GetOptionsAsync(Ct), c => c.Code == "XAB");
        Assert.False((await service.GetAsync("XAB", Ct))!.IsEnabled);
    }

    /// <summary>ERR-REF-002：被经济体使用时不能删除，提示中列出引用方</summary>
    [Fact]
    public async Task Delete_RejectsCurrencyUsedByEconomy()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new CurrencyService(db);
        await service.CreateAsync(new CurrencyInput("XAC", "测试币", "Test Dollar"), Ct);
        await new EconomyService(db).CreateAsync(new EconomyInput("QA", "测试国", "Testland", "XAC", "America/New_York", 900), Ct);

        var result = await service.DeleteAsync("XAC", Ct);

        Assert.False(result.Success);
        Assert.Contains("QA", result.Error);
        Assert.NotNull(await service.GetAsync("XAC", Ct));
    }

    /// <summary>FR-005 验收第 3 条：被指标使用时同样不能删除</summary>
    [Fact]
    public async Task Delete_RejectsCurrencyUsedByIndicator()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new CurrencyService(db);
        await service.CreateAsync(new CurrencyInput("XAE", "测试币", "Test Dollar"), Ct);
        var indicator = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(indicator, currencyCode: "XAE"), Ct);

        var result = await service.DeleteAsync("XAE", Ct);

        Assert.False(result.Success);
        Assert.Contains(indicator, result.Error);
    }

    [Fact]
    public async Task Delete_RemovesUnusedCurrency()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new CurrencyService(db);
        await service.CreateAsync(new CurrencyInput("XAD", "测试币", "Test Dollar"), Ct);

        var result = await service.DeleteAsync("XAD", Ct);

        Assert.True(result.Success, result.Error);
        Assert.Null(await service.GetAsync("XAD", Ct));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
