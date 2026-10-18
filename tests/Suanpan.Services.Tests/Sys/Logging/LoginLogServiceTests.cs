using System.Text;
using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Logging.Models;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Logging;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class LoginLogServiceTests(PostgresFixture postgres)
{
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 1, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Add_AcceptsOverlongUsernameTypedByUser()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var typed = TestData.Unique("login") + new string('x', 100);

        await new LoginLogService(db, _time).AddAsync(new SysLoginLog { Username = typed, Stage = LoginStage.Password, FailReason = "账号不存在" }, Ct);

        var stored = typed[..64];
        var saved = await db.SysLoginLogs.AsNoTracking().SingleAsync(x => x.Username == stored, Ct);
        Assert.Equal((LoginStage.Password, false, null), (saved.Stage, saved.IsSuccess, saved.UserId));
    }

    [Fact]
    public async Task QueryAndExport_FilterByUsernameAndResult()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var username = TestData.Unique("login");
        var service = new LoginLogService(db, _time);
        await service.AddAsync(new SysLoginLog { Username = username, UserId = 1, Stage = LoginStage.Totp, IsSuccess = true }, Ct);
        await service.AddAsync(new SysLoginLog { Username = username, UserId = 1, Stage = LoginStage.Password, FailReason = "密码错误" }, Ct);

        Assert.Equal(2, (await service.QueryAsync(new LoginLogQuery(username), Ct)).Total);
        var failed = Assert.Single((await service.QueryAsync(new LoginLogQuery(username, IsSuccess: false), Ct)).Items);
        Assert.Equal(LoginStage.Password, failed.Stage);

        using var output = new MemoryStream();
        Assert.Equal(1, await service.ExportAsync(new LoginLogQuery(username, IsSuccess: false), output, TimeZoneInfo.Utc, Ct));
        Assert.Contains($"{username},密码,失败,密码错误", Encoding.UTF8.GetString(output.ToArray()));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
