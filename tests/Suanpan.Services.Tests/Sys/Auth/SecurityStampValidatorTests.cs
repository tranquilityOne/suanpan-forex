using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Auth;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class SecurityStampValidatorTests(PostgresFixture postgres)
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task Validate_AcceptsOnlyCurrentStamp()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await TestData.AddUserAsync(db, Ct);
        var validator = new SecurityStampValidator(db, _cache);

        Assert.True(await validator.ValidateAsync(user.Id, user.SecurityStamp, Ct));
        Assert.False(await validator.ValidateAsync(user.Id, "other", Ct));
        Assert.False(await validator.ValidateAsync(user.Id, null, Ct));
    }

    [Theory]
    [InlineData(UserStatus.Pending, true)]
    [InlineData(UserStatus.Active, true)]
    [InlineData(UserStatus.Disabled, false)]
    public async Task Validate_RejectsDisabledAccount(UserStatus status, bool expected)
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await TestData.AddUserAsync(db, Ct, status);

        Assert.Equal(expected, await new SecurityStampValidator(db, _cache).ValidateAsync(user.Id, user.SecurityStamp, Ct));
    }

    [Fact]
    public async Task Validate_RejectsMissingAccount()
    {
        await using var db = await postgres.CreateAppContextAsync();

        Assert.False(await new SecurityStampValidator(db, _cache).ValidateAsync(999_999, "stamp", Ct));
    }

    [Fact]
    public async Task NewStamp_TakesEffectAfterInvalidate()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var user = await TestData.AddUserAsync(db, Ct);
        var validator = new SecurityStampValidator(db, _cache);
        var oldStamp = user.SecurityStamp;
        Assert.True(await validator.ValidateAsync(user.Id, oldStamp, Ct));

        await db.SysUsers.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, "new"), Ct);
        Assert.True(await validator.ValidateAsync(user.Id, oldStamp, Ct)); // 60 秒缓存内仍是旧值

        validator.Invalidate(user.Id);
        Assert.False(await validator.ValidateAsync(user.Id, oldStamp, Ct));
        Assert.True(await validator.ValidateAsync(user.Id, "new", Ct));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
