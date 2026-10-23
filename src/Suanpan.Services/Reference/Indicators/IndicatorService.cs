using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Rules;
using Suanpan.Data;
using Suanpan.Services.Common.Querying;
using Suanpan.Services.Reference.Indicators.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Reference.Indicators;

public sealed class IndicatorService(SuanpanDbContext db) : IIndicatorService
{
    public Task<PagedResult<IndicatorRow>> QueryAsync(IndicatorQuery query, CancellationToken ct = default)
        => (from i in Filter(query)
            join e in db.Economies on i.EconomyCode equals e.Code
            orderby e.Sort, i.Code
            select new IndicatorRow(i.Code, i.ShortName, i.EconomyCode, e.Name, i.Importance, i.Direction, i.Frequency, i.IsEnabled))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<Indicator?> GetAsync(string code, CancellationToken ct = default)
    {
        var normalized = ReferenceCodeRule.Normalize(code);
        return db.Indicators.AsNoTracking().FirstOrDefaultAsync(i => i.Code == normalized, ct);
    }

    public async Task<ManageResult> CreateAsync(IndicatorInput input, CancellationToken ct = default)
    {
        var economyCode = ReferenceCodeRule.Normalize(input.EconomyCode);
        var economy = await db.Economies.AsNoTracking().FirstOrDefaultAsync(e => e.Code == economyCode, ct);
        if (economy is null)
        {
            return ManageResult.Fail("经济体不存在", nameof(IndicatorInput.EconomyCode));
        }

        if (!economy.IsEnabled)
        {
            return ManageResult.Fail($"经济体 {economyCode} 已停用，不能选用", nameof(IndicatorInput.EconomyCode));
        }

        var code = ReferenceCodeRule.Normalize(input.Code);
        if (ReferenceCodeRule.ValidateIndicatorCode(code, economyCode) is { } formatError)
        {
            return ManageResult.Fail(formatError, nameof(IndicatorInput.Code));
        }

        if (await db.Indicators.AnyAsync(i => i.Code == code, ct))
        {
            return ManageResult.Fail($"指标编码 {code} 已存在", nameof(IndicatorInput.Code));
        }

        var indicator = new Indicator { Code = code, EconomyCode = economyCode, IsEnabled = true };
        Apply(indicator, input);
        if (await ValidateAsync(indicator, currencyChanged: true, ct) is { } invalid)
        {
            return invalid;
        }

        db.Indicators.Add(indicator);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> UpdateAsync(IndicatorInput input, CancellationToken ct = default)
    {
        if (await FindAsync(input.Code, ct) is not { } indicator)
        {
            return ManageResult.Fail("指标不存在");
        }

        // 校验失败时不保存，留在上下文里的改动随请求结束丢弃（操作日志用单独的 INSERT 写入，不会带上它们）
        var (currentCurrency, currentDirection) = (indicator.CurrencyCode, indicator.Direction);
        Apply(indicator, input);
        if (await ValidateAsync(indicator, currencyChanged: indicator.CurrencyCode != currentCurrency, ct) is { } invalid)
        {
            return invalid;
        }

        if (IndicatorRule.ValidateDirectionChange(currentDirection, indicator.Direction, input.Reason) is { } reasonError)
        {
            return ManageResult.Fail(reasonError, nameof(IndicatorInput.Reason));
        }

        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> SetEnabledAsync(string code, bool enabled, CancellationToken ct = default)
    {
        if (await FindAsync(code, ct) is not { } indicator)
        {
            return ManageResult.Fail("指标不存在");
        }

        indicator.IsEnabled = enabled;
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> DeleteAsync(string code, CancellationToken ct = default)
    {
        if (await FindAsync(code, ct) is not { } indicator)
        {
            return ManageResult.Fail("指标不存在");
        }

        // 引用方（基础数据设计 §3.5、BR-153）：指标与数据源的映射（JAN-79）、日历事件、快讯模板、央行加入后在这里补充，
        // 用 ReferenceUsage 拼提示
        db.Indicators.Remove(indicator);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    private IQueryable<Indicator> Filter(IndicatorQuery query)
    {
        var indicators = db.Indicators.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.EconomyCode))
        {
            var economyCode = ReferenceCodeRule.Normalize(query.EconomyCode);
            indicators = indicators.Where(i => i.EconomyCode == economyCode);
        }

        if (query.Category is { } category)
        {
            indicators = indicators.Where(i => i.Category == category);
        }

        if (query.Importance is { } importance)
        {
            indicators = indicators.Where(i => i.Importance == importance);
        }

        if (query.IsEnabled is { } isEnabled)
        {
            indicators = indicators.Where(i => i.IsEnabled == isEnabled);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var pattern = LikePattern.Contains(query.Keyword);
            indicators = indicators.Where(i => EF.Functions.ILike(i.Code, pattern, LikePattern.Escape)
                                               || EF.Functions.ILike(i.Name.Zh, pattern, LikePattern.Escape)
                                               || EF.Functions.ILike(i.Name.En, pattern, LikePattern.Escape)
                                               || EF.Functions.ILike(i.ShortName.Zh, pattern, LikePattern.Escape)
                                               || EF.Functions.ILike(i.ShortName.En, pattern, LikePattern.Escape));
        }

        return indicators;
    }

    /// <summary>货币必须存在，新选用的货币必须启用（BR-028）；字段规则见 IndicatorRule（BR-030）</summary>
    private async Task<ManageResult?> ValidateAsync(Indicator indicator, bool currencyChanged, CancellationToken ct)
    {
        var currency = await db.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == indicator.CurrencyCode, ct);
        if (currency is null)
        {
            return ManageResult.Fail("货币不存在", nameof(IndicatorInput.CurrencyCode));
        }

        if (currencyChanged && !currency.IsEnabled)
        {
            return ManageResult.Fail($"货币 {currency.Code} 已停用，不能选用", nameof(IndicatorInput.CurrencyCode));
        }

        return IndicatorRule.Validate(indicator) is { } error ? ManageResult.Fail(error.Message, error.Field) : null;
    }

    /// <summary>把输入的可修改字段写到实体上；编码和所属经济体不在这里改</summary>
    private static void Apply(Indicator indicator, IndicatorInput input)
    {
        indicator.CurrencyCode = ReferenceCodeRule.Normalize(input.CurrencyCode);
        indicator.Name = new BilingualText(input.NameZh.Trim(), input.NameEn.Trim());
        indicator.ShortName = new BilingualText(input.ShortNameZh.Trim(), input.ShortNameEn.Trim());
        indicator.Category = input.Category;
        indicator.ValueType = input.ValueType;
        indicator.Unit = input.Unit;
        indicator.Decimals = input.Decimals;
        indicator.Frequency = input.Frequency;
        indicator.Importance = input.Importance;
        indicator.Direction = input.Direction;
        indicator.FlatTolerance = input.FlatTolerance;
        indicator.RangeMin = input.RangeMin;
        indicator.RangeMax = input.RangeMax;
        indicator.SigmaN = input.SigmaN;
        indicator.Agency = new BilingualText(input.AgencyZh.Trim(), input.AgencyEn.Trim());
        indicator.DefaultReleaseTime = input.DefaultReleaseTime;
        indicator.Description = Description(input.DescriptionZh, input.DescriptionEn);
    }

    /// <summary>两种语言都没填时为 null；只填一种时另一种存空串（基础数据设计 §3.7）</summary>
    private static BilingualText? Description(string? zh, string? en)
        => string.IsNullOrWhiteSpace(zh) && string.IsNullOrWhiteSpace(en)
            ? null
            : new BilingualText(zh?.Trim() ?? string.Empty, en?.Trim() ?? string.Empty);

    private Task<Indicator?> FindAsync(string code, CancellationToken ct)
    {
        var normalized = ReferenceCodeRule.Normalize(code);
        return db.Indicators.FirstOrDefaultAsync(i => i.Code == normalized, ct);
    }
}
