using Suanpan.Data;
using Suanpan.Services.Reference.IndicatorSources;
using Suanpan.Services.Reference.IndicatorSources.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Reference;

/// <summary>
/// 指标与数据源的映射（基础数据设计 §3.8，BR-034）。字段规则的分支在 Core 的 IndicatorSourceRuleTests 中测试，
/// 这里测服务接上了规则，以及需要查库或读解析器目录的校验。解析器目录在 M1 为空，用替身
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class IndicatorSourceServiceTests(PostgresFixture postgres)
{
    /// <summary>BR-034：一个指标配置主源和备源，按优先级排列；映射到停用的数据源照样保存（ERR-IND-003 只是警告）</summary>
    [Fact]
    public async Task Create_SavesMainAndBackupSources()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var code = await AddIndicatorAsync(db);
        var main = await TestData.AddDataSourceAsync(db, Ct);
        var backup = await TestData.AddDataSourceAsync(db, Ct, enabled: false);

        var backupResult = await service.CreateAsync(Input(code, backup.Id, priority: 2), Ct);
        var mainResult = await service.CreateAsync(Input(code.ToLowerInvariant(), main.Id, priority: 1), Ct);

        Assert.True(backupResult.Success && mainResult.Success, backupResult.Error ?? mainResult.Error);
        var rows = await service.GetByIndicatorAsync(code, Ct);
        Assert.Equal([(main.Id, (short)1, true), (backup.Id, (short)2, false)], rows.Select(r => (r.SourceId, r.Priority, r.SourceEnabled)));
        Assert.Equal(StubParserCatalog.Schedule.Name, rows[0].ParserName);
    }

    /// <summary>
    /// 同一指标对同一数据源只映射一次、优先级不重复；停用的指标不能新选用（BR-028）；解析器必须在目录中（BR-012）。
    /// 都在对应的字段提示
    /// </summary>
    [Fact]
    public async Task Create_ReportsInvalidFields()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var code = await AddIndicatorAsync(db);
        var disabledCode = await AddIndicatorAsync(db, enabled: false);
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var other = await TestData.AddDataSourceAsync(db, Ct);
        await service.CreateAsync(Input(code, source.Id, priority: 1), Ct);

        var sameSource = await service.CreateAsync(Input(code, source.Id, priority: 2), Ct);
        var samePriority = await service.CreateAsync(Input(code, other.Id, priority: 1), Ct);
        var disabledIndicator = await service.CreateAsync(Input(disabledCode, other.Id, priority: 1), Ct);
        var unknownParser = await service.CreateAsync(Input(code, other.Id, priority: 2) with { ParserKey = "us.test.unknown" }, Ct);

        Assert.Equal((false, nameof(IndicatorSourceInput.SourceId)), (sameSource.Success, sameSource.Field));
        Assert.Equal((false, nameof(IndicatorSourceInput.Priority)), (samePriority.Success, samePriority.Field));
        Assert.Equal((false, nameof(IndicatorSourceInput.IndicatorCode)), (disabledIndicator.Success, disabledIndicator.Field));
        Assert.Equal((false, nameof(IndicatorSourceInput.ParserKey)), (unknownParser.Success, unknownParser.Field));
    }

    /// <summary>主备互换：备源改成 1 时与主源冲突；先把主源改成 3，备源再改成 1 就可以（修改时排除自己）</summary>
    [Fact]
    public async Task Update_ChecksPriorityAgainstOtherMappings()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var code = await AddIndicatorAsync(db);
        var main = await TestData.AddDataSourceAsync(db, Ct);
        var backup = await TestData.AddDataSourceAsync(db, Ct);
        await service.CreateAsync(Input(code, main.Id, priority: 1), Ct);
        await service.CreateAsync(Input(code, backup.Id, priority: 2), Ct);
        var ids = (await service.GetByIndicatorAsync(code, Ct)).ToDictionary(r => r.SourceId, r => r.Id);

        var conflict = await service.UpdateAsync(ids[backup.Id], Input(code, backup.Id, priority: 1), Ct);
        db.ChangeTracker.Clear();
        var moveMain = await service.UpdateAsync(ids[main.Id], Input(code, main.Id, priority: 3), Ct);
        var promote = await service.UpdateAsync(ids[backup.Id], Input(code, backup.Id, priority: 1), Ct);

        Assert.Equal((false, true, true), (conflict.Success, moveMain.Success, promote.Success));
        Assert.Equal([backup.Id, main.Id], (await service.GetByIndicatorAsync(code, Ct)).Select(r => r.SourceId));
    }

    private static IndicatorSourceService Create(SuanpanDbContext db) => new(db, new StubParserCatalog());

    private static IndicatorSourceInput Input(string indicatorCode, long sourceId, short priority) => new()
    {
        IndicatorCode = indicatorCode,
        SourceId = sourceId,
        ExternalId = " CES0000000001 ",
        ParserKey = StubParserCatalog.Schedule.Key,
        Priority = priority,
    };

    private static async Task<string> AddIndicatorAsync(SuanpanDbContext db, bool enabled = true)
    {
        var indicators = new IndicatorService(db);
        var code = TestData.IndicatorCode();
        await indicators.CreateAsync(TestData.Indicator(code), Ct);
        if (!enabled)
        {
            await indicators.SetEnabledAsync(code, false, Ct);
        }

        return code;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
