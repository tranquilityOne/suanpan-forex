using Microsoft.EntityFrameworkCore;
using Npgsql;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Logging;

/// <summary>
/// 完成标准（JAN-67，FR-029 验收第 3 条，BR-148）：应用账号对操作日志、登录日志只能追加和查询，不能修改、删除、清空。
/// 权限由迁移授予（sys 设计 §3.3），这里用应用账号直接执行 SQL 验证。
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class LogTablePermissionTests(PostgresFixture postgres)
{
    // 带上不会命中的条件：万一权限被误授予，也不会改到其他测试的数据
    [Theory]
    [InlineData("UPDATE sys_op_log SET username = 'x' WHERE id = -1")]
    [InlineData("DELETE FROM sys_op_log WHERE id = -1")]
    [InlineData("TRUNCATE sys_op_log")]
    [InlineData("UPDATE sys_login_log SET username = 'x' WHERE id = -1")]
    [InlineData("DELETE FROM sys_login_log WHERE id = -1")]
    [InlineData("TRUNCATE sys_login_log")]
    public async Task AppAccount_CannotChangeOrRemoveLogs(string sql)
    {
        await using var db = await postgres.CreateAppContextAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, Ct));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
