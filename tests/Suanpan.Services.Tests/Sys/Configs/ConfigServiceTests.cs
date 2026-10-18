using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Configs.Models;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Configs;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class ConfigServiceTests(PostgresFixture postgres)
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    // 每个测试用自己的键，避免并行执行时在唯一索引上互相等待
    private readonly string _key = TestData.Unique("test") + ":max_items";

    [Fact]
    public async Task MissingKey_ReturnsCallerDefault()
    {
        await using var db = await postgres.CreateAppContextAsync();

        Assert.Equal(5, await Service(db).GetIntAsync(_key, 5, Ct));
    }

    /// <summary>完成标准：保存后缓存立即失效</summary>
    [Fact]
    public async Task Save_TakesEffectImmediatelyDespiteCache()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Service(db);
        Assert.Equal(5, await service.GetIntAsync(_key, 5, Ct)); // "库里没有"也被缓存

        var result = await service.SaveAsync(_key, " 8 ", Ct);

        Assert.True(result.Success, result.Error);
        Assert.Equal(8, await service.GetIntAsync(_key, 5, Ct));
        var row = await db.SysConfigs.AsNoTracking().SingleAsync(c => c.ConfigKey == _key, Ct);
        Assert.Equal(("8", ConfigValueType.Int, "测试", "测试用配置"), (row.ConfigValue, row.ValueType, row.ConfigGroup, row.Description));
    }

    [Fact]
    public async Task Save_RejectsValueOfWrongTypeAndUndefinedKey()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Service(db);

        Assert.False((await service.SaveAsync(_key, "abc", Ct)).Success);
        Assert.False((await service.SaveAsync(TestData.Unique("undefined"), "1", Ct)).Success);
        Assert.False(await db.SysConfigs.AnyAsync(c => c.ConfigKey == _key, Ct));
    }

    [Fact]
    public async Task Reset_RestoresDefault()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Service(db);
        await service.SaveAsync(_key, "8", Ct);
        Assert.Equal(8, await service.GetIntAsync(_key, 5, Ct));

        await service.ResetAsync(_key, Ct);

        Assert.Equal(5, await service.GetIntAsync(_key, 5, Ct));
    }

    [Fact]
    public async Task UnparsableStoredValue_FallsBackToDefault()
    {
        await using var db = await postgres.CreateAppContextAsync();
        db.SysConfigs.Add(new SysConfig { ConfigKey = _key, ConfigValue = "abc", ValueType = ConfigValueType.Int, ConfigGroup = "测试" });
        await db.SaveChangesAsync(Ct);

        Assert.Equal(5, await Service(db).GetIntAsync(_key, 5, Ct));
    }

    [Fact]
    public async Task List_MergesDefinitionsWithStoredValues()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var orphanKey = TestData.Unique("orphan");
        db.SysConfigs.Add(new SysConfig { ConfigKey = orphanKey, ConfigValue = "x", ValueType = ConfigValueType.String, ConfigGroup = "测试" });
        await db.SaveChangesAsync(Ct);
        var service = Service(db);

        var before = Assert.Single(await service.GetListAsync(Ct), r => r.Key == _key);
        await service.SaveAsync(_key, "8", Ct);
        var after = Assert.Single(await service.GetListAsync(Ct), r => r.Key == _key);

        Assert.Equal((null, "5", true), (before.Value, before.DefaultValue, before.IsDefined));
        Assert.Equal("8", after.Value);
        Assert.False(Assert.Single(await service.GetListAsync(Ct), r => r.Key == orphanKey).IsDefined);
    }

    private ConfigService Service(SuanpanDbContext db)
        => new(db, _cache, NullLogger<ConfigService>.Instance, [new ConfigDefinition(_key, ConfigValueType.Int, "5", "测试", "测试用配置")]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
