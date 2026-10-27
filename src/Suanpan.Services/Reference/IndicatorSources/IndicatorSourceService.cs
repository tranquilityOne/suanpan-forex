using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Rules;
using Suanpan.Data;
using Suanpan.Services.Collect.Tasks;
using Suanpan.Services.Common.Querying;
using Suanpan.Services.Reference.IndicatorSources.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Reference.IndicatorSources;

public sealed class IndicatorSourceService(SuanpanDbContext db, IParserCatalog parsers) : IIndicatorSourceService
{
    public async Task<PagedResult<IndicatorSourceRow>> QueryAsync(IndicatorSourceQuery query, CancellationToken ct = default)
    {
        var mappings = db.IndicatorSources.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.IndicatorCode))
        {
            var code = ReferenceCodeRule.Normalize(query.IndicatorCode);
            mappings = mappings.Where(m => m.IndicatorCode == code);
        }

        if (query.SourceId is { } sourceId)
        {
            mappings = mappings.Where(m => m.SourceId == sourceId);
        }

        var result = await Rows(mappings).ToPagedResultAsync(query.Page, query.PageSize, ct);
        return result with { Items = WithParserNames(result.Items) };
    }

    public async Task<IReadOnlyList<IndicatorSourceRow>> GetByIndicatorAsync(string indicatorCode, CancellationToken ct = default)
    {
        var code = ReferenceCodeRule.Normalize(indicatorCode);
        return WithParserNames(await Rows(db.IndicatorSources.AsNoTracking().Where(m => m.IndicatorCode == code)).ToListAsync(ct));
    }

    public Task<IndicatorSource?> GetAsync(long id, CancellationToken ct = default)
        => db.IndicatorSources.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<ManageResult> CreateAsync(IndicatorSourceInput input, CancellationToken ct = default)
    {
        var code = ReferenceCodeRule.Normalize(input.IndicatorCode);
        var indicator = await db.Indicators.AsNoTracking().FirstOrDefaultAsync(i => i.Code == code, ct);
        if (indicator is null)
        {
            return ManageResult.Fail("指标不存在", nameof(IndicatorSourceInput.IndicatorCode));
        }

        if (!indicator.IsEnabled)
        {
            return ManageResult.Fail($"指标 {code} 已停用，不能选用", nameof(IndicatorSourceInput.IndicatorCode));
        }

        var mapping = new IndicatorSource { IndicatorCode = code };
        if (await ApplyAndValidateAsync(mapping, input, ct) is { } invalid)
        {
            return invalid;
        }

        db.IndicatorSources.Add(mapping);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> UpdateAsync(long id, IndicatorSourceInput input, CancellationToken ct = default)
    {
        if (await db.IndicatorSources.FirstOrDefaultAsync(m => m.Id == id, ct) is not { } mapping)
        {
            return ManageResult.Fail("数据源映射不存在");
        }

        // 校验失败时不保存，留在上下文里的改动随请求结束丢弃（操作日志用单独的 INSERT 写入，不会带上它们）
        if (await ApplyAndValidateAsync(mapping, input, ct) is { } invalid)
        {
            return invalid;
        }

        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default)
    {
        if (await db.IndicatorSources.FirstOrDefaultAsync(m => m.Id == id, ct) is not { } mapping)
        {
            return ManageResult.Fail("数据源映射不存在");
        }

        db.IndicatorSources.Remove(mapping);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    /// <summary>
    /// 把输入写到实体上再校验：字段规则、解析器在目录中（BR-012）、数据源存在；同一指标对同一数据源只映射一次，
    /// 优先级不重复（唯一索引兜底，这里先查出来给出字段提示）
    /// </summary>
    private async Task<ManageResult?> ApplyAndValidateAsync(IndicatorSource mapping, IndicatorSourceInput input, CancellationToken ct)
    {
        mapping.SourceId = input.SourceId;
        mapping.ExternalId = input.ExternalId.Trim();
        mapping.ParserKey = input.ParserKey.Trim();
        mapping.Priority = input.Priority;

        if (IndicatorSourceRule.Validate(mapping) is { } error)
        {
            return ManageResult.Fail(error.Message, error.Field);
        }

        if (parsers.Find(mapping.ParserKey) is null)
        {
            return ManageResult.Fail("请选择解析器", nameof(IndicatorSourceInput.ParserKey));
        }

        if (await db.DataSources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == mapping.SourceId, ct) is not { } source)
        {
            return ManageResult.Fail("数据源不存在", nameof(IndicatorSourceInput.SourceId));
        }

        var others = db.IndicatorSources.AsNoTracking().Where(m => m.IndicatorCode == mapping.IndicatorCode && m.Id != mapping.Id);
        if (await others.AnyAsync(m => m.SourceId == mapping.SourceId, ct))
        {
            return ManageResult.Fail($"指标 {mapping.IndicatorCode} 已经映射过数据源 {source.Name}", nameof(IndicatorSourceInput.SourceId));
        }

        var samePriority = await (
            from m in others
            where m.Priority == mapping.Priority
            join s in db.DataSources on m.SourceId equals s.Id
            select s.Name).FirstOrDefaultAsync(ct);
        return samePriority is null
            ? null
            : ManageResult.Fail(
                $"优先级 {mapping.Priority}（{IndicatorSourceRule.PriorityName(mapping.Priority)}）已被数据源 {samePriority} 使用，请换一个",
                nameof(IndicatorSourceInput.Priority));
    }

    private IQueryable<IndicatorSourceRow> Rows(IQueryable<IndicatorSource> mappings)
        => from m in mappings
           join i in db.Indicators on m.IndicatorCode equals i.Code
           join s in db.DataSources on m.SourceId equals s.Id
           orderby m.IndicatorCode, m.Priority
           select new IndicatorSourceRow(m.Id, m.IndicatorCode, i.ShortName, m.SourceId, s.Name, s.IsEnabled, m.ExternalId, m.ParserKey, m.Priority);

    private List<IndicatorSourceRow> WithParserNames(IEnumerable<IndicatorSourceRow> rows)
        => rows.Select(row => row with { ParserName = parsers.Find(row.ParserKey)?.Name }).ToList();
}
