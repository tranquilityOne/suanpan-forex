using System.Text;
using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Logging.Models;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Logging;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class OpLogServiceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 18, 1, 0, 0, TimeSpan.Zero);

    private readonly FixedTimeProvider _time = new(Morning);
    private readonly string _user = TestData.Unique("op");

    [Fact]
    public async Task Add_StampsUtcTimeAndCutsOverlongFields()
    {
        await using var db = await postgres.CreateAppContextAsync();

        await Service(db).AddAsync(Log(_user, errorMsg: new string('x', 600)), Ct);

        var saved = await db.SysOpLogs.AsNoTracking().SingleAsync(x => x.Username == _user, Ct);
        Assert.Equal(Morning.UtcDateTime, saved.CreatedAt);
        Assert.Equal(512, saved.ErrorMsg!.Length);
    }

    [Fact]
    public async Task Add_DoesNotSavePendingChangesOfTheRequest()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var pendingRole = new SysRole { RoleCode = TestData.Unique("pending"), RoleName = "未保存" };
        db.SysRoles.Add(pendingRole); // 模拟业务操作失败后留在上下文中的改动

        await Service(db).AddAsync(Log(_user), Ct);

        Assert.False(await db.SysRoles.AsNoTracking().AnyAsync(r => r.RoleCode == pendingRole.RoleCode, Ct));
    }

    [Fact]
    public async Task Query_FiltersByUserModuleActionResultAndTime()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Service(db);
        await service.AddAsync(Log(_user, action: "停用账号"), Ct);
        await service.AddAsync(Log(_user, action: "重置密码", isSuccess: false), Ct);
        _time.UtcNow = Morning.AddHours(2);
        await service.AddAsync(Log(_user, action: "停用账号"), Ct);

        Assert.Equal(3, (await service.QueryAsync(new OpLogQuery(_user.ToUpperInvariant()), Ct)).Total);
        Assert.Equal(2, (await service.QueryAsync(new OpLogQuery(_user, "系统管理", "停用账号"), Ct)).Total);
        Assert.Equal(1, (await service.QueryAsync(new OpLogQuery(_user, IsSuccess: false), Ct)).Total);
        var early = await service.QueryAsync(new OpLogQuery(_user, Begin: Morning.UtcDateTime, End: Morning.AddHours(1).UtcDateTime), Ct);
        Assert.Equal(2, early.Total);
    }

    [Fact]
    public async Task Export_WritesNewestFirstInOperatorTimeZone()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Service(db);
        await service.AddAsync(Log(_user, action: "停用账号"), Ct);
        _time.UtcNow = Morning.AddHours(2);
        await service.AddAsync(Log(_user, action: "重置密码"), Ct);
        using var output = new MemoryStream();

        var count = await service.ExportAsync(new OpLogQuery(_user), output, TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"), Ct);

        Assert.Equal(2, count);
        var lines = Encoding.UTF8.GetString(output.ToArray()).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("时间（Asia/Shanghai）,", lines[0]);
        Assert.StartsWith($"2026-10-18 11:00:00,{_user},", lines[1]); // UTC 03:00 → 北京时间 11:00，最新的在前
        Assert.Contains("重置密码", lines[1]);
        Assert.StartsWith("2026-10-18 09:00:00,", lines[2]);
    }

    private OpLogService Service(SuanpanDbContext db) => new(db, _time);

    private static SysOpLog Log(string username, string action = "停用账号", bool isSuccess = true, string? errorMsg = null) => new()
    {
        UserId = 1,
        Username = username,
        Roles = "super_admin",
        Module = "系统管理",
        Action = action,
        HttpMethod = "POST",
        RequestUrl = "/Sys/User/SetStatus",
        IsSuccess = isSuccess,
        ErrorMsg = errorMsg,
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
