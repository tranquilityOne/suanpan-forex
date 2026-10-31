using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Suanpan.Collector.Execution;
using Suanpan.Collector.Watch;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Data;
using Suanpan.Infrastructure.Http;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Services.Calendar.Releases;
using Suanpan.Services.Collect.Ingest;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Common.Alerts;
using Suanpan.Shared.Messages.Collect;

namespace Suanpan.Collector.Tests.Fixtures;

/// <summary>
/// 在内存中启动的 Collector（WebApplicationFactory）：连测试容器，采集用的 HttpClient 接到 <see cref="StubHttp"/>，
/// 另注册测试用的适配器、解析器和入库服务（<see cref="FakeAdapter"/> 等），快照服务可以按数据源模拟写入失败，告警被记录下来。
/// 消息接收、限流、分布式锁都是真实的
/// </summary>
public sealed class CollectorApp(string connectionString, string redis, string rabbitMq, StubHttp http) : WebApplicationFactory<Program>
{
    /// <summary>配置 Collect:ContactEmail 的值，采集请求的 User-Agent 中应带上它</summary>
    public const string ContactEmail = "collector-test@example.com";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Main", connectionString);
        builder.UseSetting("Redis:Configuration", redis);
        builder.UseSetting("RabbitMq:Uri", rabbitMq);
        builder.UseSetting("RabbitMq:RetryDelaySeconds", "1");
        builder.UseSetting("Security:EncryptionKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Collect:ContactEmail", ContactEmail);
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ICollectAdapter, FakeAdapter>();
            services.AddSingleton<IParser, FakeParser>();
            // 换掉真实的入库服务：执行框架的测试用假的日历排期入库（日历的入库在 Services.Tests 中测试）；
            // 公布值用真实的入库服务，盯盘的测试按事件状态判断拿到值
            services.RemoveAll<ICollectIngestor>();
            services.AddSingleton<ICollectIngestor, FakeIngestor>();
            services.AddScoped<ICollectIngestor, CalendarReleaseIngestor>();

            // 盯盘的后台服务按真实时钟运行，测试中去掉，由盯盘的测试按手动推进的时钟自己调用
            services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(WatchService)));
            services.AddSingleton<SnapshotFaults>();
            services.AddScoped<SnapshotService>();
            services.AddScoped<ISnapshotService>(sp => new FaultySnapshotService(sp.GetRequiredService<SnapshotService>(), sp.GetRequiredService<SnapshotFaults>()));
            services.AddSingleton<RecordingAlertService>();
            services.AddSingleton<IAlertService>(sp => sp.GetRequiredService<RecordingAlertService>());
            services.AddHttpClient(CollectHttp.ClientName).ConfigurePrimaryHttpMessageHandler(http.CreateHandler);
        });
    }

    /// <summary>启用的数据源和它下面一个停用的任务（停用的任务可以手动触发，BR-155）；数据源的基础地址指向 StubHttp 的主机</summary>
    public async Task<CollectTask> AddTaskAsync(string host, int rateLimit = 100, string? parameters = null, int? dailyLimit = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SuanpanDbContext>();
        var source = new DataSource
        {
            Name = $"源_{Guid.NewGuid():N}",
            Agency = "测试机构",
            Type = DataSourceType.OfficialApi,
            BaseUrl = $"https://{host}/",
            RateLimit = rateLimit,
            RateLimitUnit = RateLimitUnit.PerSecond,
            DailyLimit = dailyLimit,
            ComplianceClass = ComplianceClass.GovernmentPublic,
            TermsUrl = $"https://{host}/terms",
            RobotsResult = RobotsResult.NotApplicable,
            IsEnabled = true,
        };
        db.DataSources.Add(source);
        await db.SaveChangesAsync();

        var task = new CollectTask
        {
            Name = $"任务_{Guid.NewGuid():N}",
            SourceId = source.Id,
            ParserKey = FakeParser.ParserKey,
            TargetType = CollectTargetType.Calendar,
            Params = parameters,
        };
        db.CollectTasks.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    /// <summary>后台的"立即执行"：创建排队的运行记录并下发（与 Crm 走同一个服务）</summary>
    public async Task<long> TriggerAsync(CollectTask task)
    {
        await using var scope = Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ICollectRunService>().TriggerAsync(task.Id, "tester", anyTargetType: true);
        return result is { Success: true, Published: true, RunId: { } runId }
            ? runId
            : throw new InvalidOperationException($"立即执行失败：{result.Error}");
    }

    public FakeIngestor Ingestor => Services.GetServices<ICollectIngestor>().OfType<FakeIngestor>().Single();

    public SnapshotFaults SnapshotFaults => Services.GetRequiredService<SnapshotFaults>();

    public RecordingAlertService Alerts => Services.GetRequiredService<RecordingAlertService>();

    /// <summary>运行记录的快照，按第几次请求、响应序号排序</summary>
    public async Task<List<Snapshot>> GetSnapshotsAsync(long runId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SuanpanDbContext>().Snapshots.AsNoTracking()
            .Where(s => s.RunId == runId).OrderBy(s => s.Attempt).ThenBy(s => s.Seq).ToListAsync();
    }

    /// <summary>直接发一条指令，模拟重复投递</summary>
    public Task PublishAsync(long runId, int attempt = 1)
        => Services.GetRequiredService<IEventBus>().PublishAsync(new CollectRunCommand(runId, attempt));

    /// <summary>
    /// 后台发起重新解析的效果：创建排队的重新解析记录并下发 ReparseCommand。直接写库，不经过 ICollectRunService（它按解析器目录校验，
    /// 目录中没有测试用的解析器）；发起时的校验在 Suanpan.Services.Tests 中测试
    /// </summary>
    public async Task<long> ReparseAsync(CollectTask task, params long[] snapshotIds)
    {
        var run = new CollectRun
        {
            TaskId = task.Id,
            SourceId = task.SourceId,
            Trigger = CollectRunTrigger.Reparse,
            TriggeredBy = "tester",
            Status = CollectRunStatus.Queued,
            QueuedAt = DateTime.UnixEpoch,
            Reparse = new CollectRunReparse { ParserKey = task.ParserKey, SnapshotIds = [.. snapshotIds.Order()] },
        };
        await ExecuteAsync(async db =>
        {
            db.CollectRuns.Add(run);
            await db.SaveChangesAsync();
        });
        await Services.GetRequiredService<IEventBus>().PublishAsync(new ReparseCommand(run.Id));
        return run.Id;
    }

    public async Task<CollectRun> GetRunAsync(long runId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SuanpanDbContext>().CollectRuns.AsNoTracking().SingleAsync(r => r.Id == runId);
    }

    /// <summary>等运行记录到达某个状态</summary>
    public Task<CollectRun> WaitForRunAsync(long runId, Func<CollectRun, bool> done)
        => Eventually.GetAsync(() => GetRunAsync(runId), done, r => $"状态 {r.Status}，请求 {r.AttemptCount} 次，错误 {r.ErrorCode} {r.ErrorMessage}");

    public Task<CollectRun> WaitUntilFinishedAsync(long runId)
        => WaitForRunAsync(runId, r => CollectRunStatusRule.IsFinished(r.Status));

    public async Task ExecuteAsync(Func<SuanpanDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<SuanpanDbContext>());
    }
}
