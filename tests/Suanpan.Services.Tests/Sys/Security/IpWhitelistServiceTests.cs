using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Entities;
using Suanpan.Services.Sys.Security;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Security;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class IpWhitelistServiceTests(PostgresFixture postgres)
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task Save_AddsTrimmedEntryAndFillsAuditFields()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IpWhitelistService(db, _cache);

        var result = await service.SaveAsync(new SysIpWhitelist { IpValue = " 203.0.113.0/24 ", Remark = "  办公室网络 " }, Ct);

        Assert.True(result.Success, result.Error);
        var saved = await db.SysIpWhitelists.AsNoTracking().SingleAsync(x => x.IpValue == "203.0.113.0/24", Ct);
        Assert.Equal("办公室网络", saved.Remark);
        Assert.Equal("test", saved.CreatedBy);
    }

    [Fact]
    public async Task Save_RejectsInvalidFormat()
    {
        await using var db = await postgres.CreateAppContextAsync();

        var result = await new IpWhitelistService(db, _cache).SaveAsync(new SysIpWhitelist { IpValue = "10.1" }, Ct);

        Assert.False(result.Success);
        Assert.False(await db.SysIpWhitelists.AnyAsync(x => x.IpValue == "10.1", Ct));
    }

    [Fact]
    public async Task Save_RejectsDuplicate()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IpWhitelistService(db, _cache);
        await service.SaveAsync(new SysIpWhitelist { IpValue = "203.0.113.10" }, Ct);

        var result = await service.SaveAsync(new SysIpWhitelist { IpValue = "203.0.113.10" }, Ct);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Update_ChangesExistingEntry()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IpWhitelistService(db, _cache);
        await service.SaveAsync(new SysIpWhitelist { IpValue = "203.0.113.10" }, Ct);
        var id = (await db.SysIpWhitelists.AsNoTracking().SingleAsync(x => x.IpValue == "203.0.113.10", Ct)).Id;

        var result = await service.SaveAsync(new SysIpWhitelist { Id = id, IpValue = "203.0.113.11", IsEnabled = false }, Ct);

        Assert.True(result.Success, result.Error);
        var saved = await db.SysIpWhitelists.AsNoTracking().SingleAsync(x => x.Id == id, Ct);
        Assert.Equal("203.0.113.11", saved.IpValue);
        Assert.False(saved.IsEnabled);
        Assert.Equal("test", saved.UpdatedBy);
    }

    [Fact]
    public async Task IsAllowed_UsesOnlyEnabledEntries()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IpWhitelistService(db, _cache);
        await service.SaveAsync(new SysIpWhitelist { IpValue = "203.0.113.0/24" }, Ct);
        await service.SaveAsync(new SysIpWhitelist { IpValue = "198.51.100.7", IsEnabled = false }, Ct);

        Assert.True(await service.IsAllowedAsync(IPAddress.Parse("203.0.113.99"), Ct));
        Assert.False(await service.IsAllowedAsync(IPAddress.Parse("198.51.100.7"), Ct));
        Assert.False(await service.IsAllowedAsync(IPAddress.Parse("192.0.2.1"), Ct));
    }

    [Fact]
    public async Task SaveAndDelete_InvalidateCacheImmediately()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = new IpWhitelistService(db, _cache);
        var client = IPAddress.Parse("203.0.113.10");

        Assert.False(await service.IsAllowedAsync(client, Ct)); // 空名单被缓存

        await service.SaveAsync(new SysIpWhitelist { IpValue = "203.0.113.10" }, Ct);
        Assert.True(await service.IsAllowedAsync(client, Ct));

        var id = (await db.SysIpWhitelists.AsNoTracking().SingleAsync(x => x.IpValue == "203.0.113.10", Ct)).Id;
        Assert.True((await service.DeleteAsync(id, Ct)).Success);
        Assert.False(await service.IsAllowedAsync(client, Ct));
    }

    [Fact]
    public async Task Delete_ReportsMissingEntry()
    {
        await using var db = await postgres.CreateAppContextAsync();

        var result = await new IpWhitelistService(db, _cache).DeleteAsync(999_999, Ct);

        Assert.False(result.Success);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
