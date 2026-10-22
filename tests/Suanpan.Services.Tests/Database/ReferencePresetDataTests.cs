using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Reference.Rules;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Database;

/// <summary>
/// FR-005 验收第 1 条：执行迁移后有 10 个经济体及其货币（基础数据设计 §2.4）。
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

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
