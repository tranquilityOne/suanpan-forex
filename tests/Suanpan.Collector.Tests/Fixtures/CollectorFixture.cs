using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RabbitMQ.Client;
using Suanpan.Data;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Sys.Configs;
using Suanpan.Shared.Messages.Collect;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

[assembly: AssemblyFixture(typeof(Suanpan.Collector.Tests.Fixtures.CollectorFixture))]

namespace Suanpan.Collector.Tests.Fixtures;

/// <summary>
/// Collector 集成测试的环境（采集设计 §12）：Testcontainers 启动 PostgreSQL 17、Valkey 8.1、RabbitMQ 4.1（需要 Docker），
/// 按生产方式建好数据库账号并执行迁移，再在内存中启动 Collector（<see cref="CollectorApp"/>），重试间隔配置为 0 秒。
/// 整个测试程序集共用一套，在第一个用到它的测试中才启动（只跑单元测试时不启动）。
/// Collector 在后台自己提交事务，测试不能用回滚隔离（后端约定 §13.4 的例外），改为每个测试用自己的数据源、任务和主机名
/// </summary>
public sealed class CollectorFixture : IAsyncLifetime
{
    private const string Database = "suanpan";

    // 每次运行随机生成，只用于临时容器
    private readonly string _password = Guid.NewGuid().ToString("N");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly RedisContainer _valkey = new RedisBuilder("valkey/valkey:8.1-alpine").Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();

    private readonly Lazy<Task<CollectorApp>> _app;

    public CollectorFixture() => _app = new Lazy<Task<CollectorApp>>(StartAsync);

    public StubHttp Http { get; } = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>运行中的 Collector（第一次调用时启动容器和程序）</summary>
    public Task<CollectorApp> GetAppAsync() => _app.Value;

    private async Task<CollectorApp> StartAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _valkey.StartAsync(), _rabbitMq.StartAsync());
        await CreateDatabaseAsync();
        await DeclareQueueAsync();

        var app = new CollectorApp(ConnectionStringFor("suanpan_app"), _valkey.GetConnectionString(), _rabbitMq.GetConnectionString(), Http);
        await using var scope = app.Services.CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<IConfigService>().SaveAsync(CollectConfigKeys.RetryDelaySeconds, "0,0,0");
        if (!saved.Success)
        {
            throw new InvalidOperationException($"重试间隔配置失败：{saved.Error}");
        }

        return app;
    }

    /// <summary>与生产相同：三个账号，迁移账号建库并执行全部迁移，Collector 用应用账号连接</summary>
    private async Task CreateDatabaseAsync()
    {
        await using (var admin = new NpgsqlConnection(_postgres.GetConnectionString()))
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

        await using var migrator = new SuanpanDbContext(
            new DbContextOptionsBuilder<SuanpanDbContext>().UseNpgsql(ConnectionStringFor("suanpan_migrator")).Options);
        await migrator.Database.MigrateAsync();
    }

    /// <summary>预先声明 Collector 的接收队列：接收端在后台启动，消息不会在它就绪之前因没有队列而丢失</summary>
    private async Task DeclareQueueAsync()
    {
        await using var connection = await new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var messageName = nameof(CollectRunCommand);
        var queue = MessageQueueNames.Queue("collector", messageName);
        await channel.ExchangeDeclareAsync(MessageQueueNames.Exchange, ExchangeType.Direct, durable: true, autoDelete: false);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queue, MessageQueueNames.Exchange, messageName);
    }

    private string ConnectionStringFor(string username) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = Database,
            Username = username,
            Password = _password,
        }.ConnectionString;

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app.IsValueCreated)
        {
            await (await _app.Value).DisposeAsync();
            await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _valkey.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
        }
    }
}
