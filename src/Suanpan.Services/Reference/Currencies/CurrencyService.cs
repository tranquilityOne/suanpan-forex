using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Rules;
using Suanpan.Data;
using Suanpan.Services.Reference.Common;
using Suanpan.Services.Reference.Currencies.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Reference.Currencies;

public sealed class CurrencyService(SuanpanDbContext db) : ICurrencyService
{
    public async Task<IReadOnlyList<CurrencyRow>> GetListAsync(CancellationToken ct = default)
    {
        var currencies = await db.Currencies.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
        var economyCounts = await db.Economies
            .GroupBy(e => e.CurrencyCode)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Code, x => x.Count, ct);
        var indicatorCounts = await db.Indicators
            .GroupBy(i => i.CurrencyCode)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Code, x => x.Count, ct);

        return currencies
            .Select(c => new CurrencyRow(c.Code, c.Name, c.IsEnabled, economyCounts.GetValueOrDefault(c.Code), indicatorCounts.GetValueOrDefault(c.Code)))
            .ToList();
    }

    public Task<Currency?> GetAsync(string code, CancellationToken ct = default)
    {
        var normalized = ReferenceCodeRule.Normalize(code);
        return db.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == normalized, ct);
    }

    public async Task<IReadOnlyList<Currency>> GetOptionsAsync(CancellationToken ct = default)
        => await db.Currencies.AsNoTracking().Where(c => c.IsEnabled).OrderBy(c => c.Code).ToListAsync(ct);

    public async Task<ManageResult> CreateAsync(CurrencyInput input, CancellationToken ct = default)
    {
        var code = ReferenceCodeRule.Normalize(input.Code);
        if (ReferenceCodeRule.ValidateCurrencyCode(code) is { } formatError)
        {
            return ManageResult.Fail(formatError, nameof(CurrencyInput.Code));
        }

        if (await db.Currencies.AnyAsync(c => c.Code == code, ct))
        {
            return ManageResult.Fail($"货币编码 {code} 已存在", nameof(CurrencyInput.Code));
        }

        db.Currencies.Add(new Currency { Code = code, Name = Name(input), IsEnabled = true });
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> UpdateAsync(CurrencyInput input, CancellationToken ct = default)
    {
        if (await FindAsync(input.Code, ct) is not { } currency)
        {
            return ManageResult.Fail("货币不存在");
        }

        currency.Name = Name(input);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> SetEnabledAsync(string code, bool enabled, CancellationToken ct = default)
    {
        if (await FindAsync(code, ct) is not { } currency)
        {
            return ManageResult.Fail("货币不存在");
        }

        currency.IsEnabled = enabled;
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> DeleteAsync(string code, CancellationToken ct = default)
    {
        if (await FindAsync(code, ct) is not { } currency)
        {
            return ManageResult.Fail("货币不存在");
        }

        // 引用方（基础数据设计 §3.5）：央行、参考汇率等模块加入后在这里补充
        var economies = await db.Economies.AsNoTracking()
            .Where(e => e.CurrencyCode == currency.Code)
            .OrderBy(e => e.Sort)
            .Select(e => e.Code)
            .ToListAsync(ct);
        var indicators = await db.Indicators.AsNoTracking()
            .Where(i => i.CurrencyCode == currency.Code)
            .OrderBy(i => i.Code)
            .Select(i => i.Code)
            .ToListAsync(ct);
        if (ReferenceUsage.DescribeOrNull(("经济体", economies), ("指标", indicators)) is { } usage)
        {
            return ManageResult.Fail(usage);
        }

        db.Currencies.Remove(currency);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    private Task<Currency?> FindAsync(string code, CancellationToken ct)
    {
        var normalized = ReferenceCodeRule.Normalize(code);
        return db.Currencies.FirstOrDefaultAsync(c => c.Code == normalized, ct);
    }

    private static BilingualText Name(CurrencyInput input) => new(input.NameZh.Trim(), input.NameEn.Trim());
}
