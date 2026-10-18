using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Suanpan.Infrastructure;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Database;

/// <summary>
/// Hangfire 用应用账号在 hangfire schema 中建表：依赖第一个迁移授予的 CREATE、USAGE 权限（数据库变更约定 §2.4）。
/// 授权写错或 Hangfire 升级后需要更多权限时，Job 会在启动时失败，这里提前发现。
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class HangfireSchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task HangfireInstallsItsTablesWithAppAccount()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Main"] = await postgres.GetAppConnectionStringAsync(),
        }).Build();
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSuanpanHangfire(configuration, runServer: false)
            .BuildServiceProvider();

        var storage = provider.GetRequiredService<JobStorage>();

        Assert.Equal(0, storage.GetMonitoringApi().EnqueuedCount("default"));
    }
}
