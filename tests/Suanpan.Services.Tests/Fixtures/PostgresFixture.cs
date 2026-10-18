using Microsoft.EntityFrameworkCore;
using Npgsql;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(typeof(Suanpan.Services.Tests.Fixtures.PostgresFixture))]

namespace Suanpan.Services.Tests.Fixtures;

/// <summary>
/// 测试用 PostgreSQL 17：用 Testcontainers 启动临时容器（需要 Docker），按生产方式建好三个账号，
/// 用迁移账号执行全部迁移；测试用应用账号连接，权限与线上一致（docs/conventions/backend.md §13）。
/// 整个测试程序集共用一个容器，每个测试在自己的事务里执行，结束时回滚。
/// 容器在第一个用到数据库的测试中才启动：xUnit 不论筛选条件都会创建程序集夹具，
/// 只跑单元测试（--filter-not-trait "Category=Integration"）时不应启动容器。
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string Database = "suanpan";

    // 每次运行随机生成，只用于临时容器
    private readonly string _password = Guid.NewGuid().ToString("N");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly Lazy<Task<string>> _appConnectionString;

    public PostgresFixture() => _appConnectionString = new Lazy<Task<string>>(StartAsync);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>应用账号 suanpan_app 的连接串（第一次调用时启动容器并执行迁移）</summary>
    public Task<string> GetAppConnectionStringAsync() => _appConnectionString.Value;

    private async Task<string> StartAsync()
    {
        await _container.StartAsync();

        await using (var admin = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await admin.OpenAsync();
            foreach (var role in new[] { "suanpan_migrator", "suanpan_app", "suanpan_portal" })
            {
                await ExecuteAsync(admin, $"CREATE ROLE {role} LOGIN PASSWORD '{_password}'");
            }

            await ExecuteAsync(admin, $"CREATE DATABASE {Database} OWNER suanpan_migrator");
            await ExecuteAsync(admin, $"REVOKE ALL ON DATABASE {Database} FROM PUBLIC");
            await ExecuteAsync(admin, $"GRANT CONNECT ON DATABASE {Database} TO suanpan_app, suanpan_portal");
        }

        await using (var migrator = new SuanpanDbContext(Options(ConnectionStringFor("suanpan_migrator"))))
        {
            await migrator.Database.MigrateAsync();
        }

        return ConnectionStringFor("suanpan_app");
    }

    /// <summary>
    /// 用应用账号创建上下文并开启事务；释放上下文时事务回滚，测试之间互不影响。
    /// 审计字段的操作人固定为 test。
    /// </summary>
    public async Task<SuanpanDbContext> CreateAppContextAsync(TimeProvider? timeProvider = null)
    {
        var db = new SuanpanDbContext(new DbContextOptionsBuilder<SuanpanDbContext>()
            .UseNpgsql(await GetAppConnectionStringAsync())
            .AddInterceptors(new AuditInterceptor(new FixedAuditActorProvider("test"), timeProvider ?? TimeProvider.System))
            .Options);
        await db.Database.BeginTransactionAsync();
        return db;
    }

    public async ValueTask DisposeAsync()
    {
        if (_appConnectionString.IsValueCreated)
        {
            await _container.DisposeAsync();
        }
    }

    private string ConnectionStringFor(string username) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = Database,
            Username = username,
            Password = _password,
        }.ConnectionString;

    private static DbContextOptions<SuanpanDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<SuanpanDbContext>().UseNpgsql(connectionString).Options;

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
