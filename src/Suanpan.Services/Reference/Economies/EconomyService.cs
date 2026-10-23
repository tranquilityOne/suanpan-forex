using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Rules;
using Suanpan.Data;
using Suanpan.Services.Reference.Common;
using Suanpan.Services.Reference.Economies.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Reference.Economies;

public sealed class EconomyService(SuanpanDbContext db) : IEconomyService
{
    private const short SortStep = 10;

    public async Task<IReadOnlyList<EconomyRow>> GetListAsync(CancellationToken ct = default)
    {
        var economies = await db.Economies.AsNoTracking().OrderBy(e => e.Sort).ThenBy(e => e.Code).ToListAsync(ct);
        var indicatorCounts = await db.Indicators
            .GroupBy(i => i.EconomyCode)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Code, x => x.Count, ct);
        var sourceCounts = await db.DataSources
            .Where(s => s.EconomyCode != null)
            .GroupBy(s => s.EconomyCode!)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Code, x => x.Count, ct);

        return economies
            .Select(e => new EconomyRow(
                e.Code, e.Name, e.CurrencyCode, e.TimeZone, e.Sort, e.IsEnabled,
                indicatorCounts.GetValueOrDefault(e.Code), sourceCounts.GetValueOrDefault(e.Code)))
            .ToList();
    }

    public Task<Economy?> GetAsync(string code, CancellationToken ct = default)
    {
        var normalized = ReferenceCodeRule.Normalize(code);
        return db.Economies.AsNoTracking().FirstOrDefaultAsync(e => e.Code == normalized, ct);
    }

    public async Task<IReadOnlyList<Economy>> GetOptionsAsync(CancellationToken ct = default)
        => await db.Economies.AsNoTracking().Where(e => e.IsEnabled).OrderBy(e => e.Sort).ThenBy(e => e.Code).ToListAsync(ct);

    public async Task<short> GetNextSortAsync(CancellationToken ct = default)
    {
        var max = await db.Economies.MaxAsync(e => (short?)e.Sort, ct) ?? 0;
        return (short)Math.Min(max + SortStep, short.MaxValue);
    }

    public async Task<ManageResult> CreateAsync(EconomyInput input, CancellationToken ct = default)
    {
        var code = ReferenceCodeRule.Normalize(input.Code);
        if (ReferenceCodeRule.ValidateEconomyCode(code) is { } formatError)
        {
            return ManageResult.Fail(formatError, nameof(EconomyInput.Code));
        }

        if (await db.Economies.AnyAsync(e => e.Code == code, ct))
        {
            return ManageResult.Fail($"经济体编码 {code} 已存在", nameof(EconomyInput.Code));
        }

        var currencyCode = ReferenceCodeRule.Normalize(input.CurrencyCode);
        if (await ValidateAsync(currencyCode, input.TimeZone, currencyChanged: true, ct) is { } invalid)
        {
            return invalid;
        }

        db.Economies.Add(new Economy
        {
            Code = code,
            Name = Name(input),
            CurrencyCode = currencyCode,
            TimeZone = input.TimeZone.Trim(),
            Sort = input.Sort,
            IsEnabled = true,
        });
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> UpdateAsync(EconomyInput input, CancellationToken ct = default)
    {
        if (await FindAsync(input.Code, ct) is not { } economy)
        {
            return ManageResult.Fail("经济体不存在");
        }

        var currencyCode = ReferenceCodeRule.Normalize(input.CurrencyCode);
        if (await ValidateAsync(currencyCode, input.TimeZone, currencyChanged: currencyCode != economy.CurrencyCode, ct) is { } invalid)
        {
            return invalid;
        }

        economy.Name = Name(input);
        economy.CurrencyCode = currencyCode;
        economy.TimeZone = input.TimeZone.Trim();
        economy.Sort = input.Sort;
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> SetEnabledAsync(string code, bool enabled, CancellationToken ct = default)
    {
        if (await FindAsync(code, ct) is not { } economy)
        {
            return ManageResult.Fail("经济体不存在");
        }

        economy.IsEnabled = enabled;
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> DeleteAsync(string code, CancellationToken ct = default)
    {
        if (await FindAsync(code, ct) is not { } economy)
        {
            return ManageResult.Fail("经济体不存在");
        }

        // 引用方（基础数据设计 §3.5）：央行等模块加入后在这里补充
        var indicators = await db.Indicators.AsNoTracking()
            .Where(i => i.EconomyCode == economy.Code)
            .OrderBy(i => i.Code)
            .Select(i => i.Code)
            .ToListAsync(ct);
        var sources = await db.DataSources.AsNoTracking()
            .Where(s => s.EconomyCode == economy.Code)
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync(ct);
        if (ReferenceUsage.DescribeOrNull(("指标", indicators), ("数据源", sources)) is { } usage)
        {
            return ManageResult.Fail(usage);
        }

        db.Economies.Remove(economy);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    /// <summary>货币必须存在；新选用的货币必须启用（BR-028）；时区必须是 IANA 名称（BR-027、ERR-REF-003）</summary>
    private async Task<ManageResult?> ValidateAsync(string currencyCode, string timeZone, bool currencyChanged, CancellationToken ct)
    {
        var currency = await db.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == currencyCode, ct);
        if (currency is null)
        {
            return ManageResult.Fail("货币不存在", nameof(EconomyInput.CurrencyCode));
        }

        if (currencyChanged && !currency.IsEnabled)
        {
            return ManageResult.Fail($"货币 {currencyCode} 已停用，不能选用", nameof(EconomyInput.CurrencyCode));
        }

        if (!TimeZoneRule.IsValid(timeZone.Trim()))
        {
            return ManageResult.Fail("时区必须是 IANA 时区名称，如 America/New_York", nameof(EconomyInput.TimeZone));
        }

        return null;
    }

    private Task<Economy?> FindAsync(string code, CancellationToken ct)
    {
        var normalized = ReferenceCodeRule.Normalize(code);
        return db.Economies.FirstOrDefaultAsync(e => e.Code == normalized, ct);
    }

    private static BilingualText Name(EconomyInput input) => new(input.NameZh.Trim(), input.NameEn.Trim());
}
