using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Suanpan.Data.DesignTime;

/// <summary>
/// dotnet ef 命令使用的设计时工厂：Data 自己作为启动项目（docs/conventions/database.md §3.1）。
/// 连接串取 ConnectionStrings:Migrator（迁移账号 suanpan_migrator），来自四个程序共用的 user-secrets
/// 或环境变量 SUANPAN__ConnectionStrings__Migrator。生成迁移、导出脚本时不连接数据库，可以不设置。
/// </summary>
public sealed class SuanpanDbContextFactory : IDesignTimeDbContextFactory<SuanpanDbContext>
{
    // 与 Directory.Build.props 中四个程序共用的 UserSecretsId 一致
    private const string UserSecretsId = "suanpan-forex";

    // 只用于不连接数据库的命令：migrations add、migrations script、has-pending-model-changes
    private const string OfflineConnectionString = "Host=127.0.0.1;Database=suanpan";

    public SuanpanDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(UserSecretsId)
            .AddEnvironmentVariables(prefix: "SUANPAN__")
            .Build();

        var connectionString = configuration.GetConnectionString("Migrator");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("未设置 ConnectionStrings:Migrator，只能执行不连接数据库的命令；执行迁移前请先设置（见 README）。");
            connectionString = OfflineConnectionString;
        }

        return new SuanpanDbContext(new DbContextOptionsBuilder<SuanpanDbContext>().UseNpgsql(connectionString).Options);
    }
}
