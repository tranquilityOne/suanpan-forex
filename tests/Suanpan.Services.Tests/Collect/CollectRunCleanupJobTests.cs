using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Services.Collect.Jobs;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Configs.Models;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Mvc;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>运行记录的清理（采集设计 §4.5，BR-011）：按系统配置的保留天数分批删除，断言只看本测试建的数据</summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CollectRunCleanupJobTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 10, 27, 20, 0, 0, DateTimeKind.Utc);

    /// <summary>保留天数改为 30 时删除 30 天前的记录；每批 2 条，3 条旧记录要删两批</summary>
    [Fact]
    public async Task Cleanup_DeletesRunsOlderThanRetentionInBatches()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id);
        var kept = Run(task, Now.AddDays(-29));
        db.CollectRuns.AddRange(kept, Run(task, Now.AddDays(-31)), Run(task, Now.AddDays(-40)), Run(task, Now.AddDays(-100)));
        await db.SaveChangesAsync(Ct);
        var job = new CollectRunCleanupJob(
            db, new StubConfigService(retentionDays: 30), new StubDistributedLock(), new FixedTimeProvider(new DateTimeOffset(Now)),
            NullLogger<CollectRunCleanupJob>.Instance, batchSize: 2);

        await job.RunAsync(Ct);

        Assert.Equal([kept.Id], await db.CollectRuns.Where(r => r.TaskId == task.Id).Select(r => r.Id).ToListAsync(Ct));
    }

    private static CollectRun Run(CollectTask task, DateTime queuedAt) => new()
    {
        TaskId = task.Id,
        SourceId = task.SourceId,
        Trigger = CollectRunTrigger.Manual,
        Status = CollectRunStatus.Succeeded,
        QueuedAt = queuedAt,
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>只给出运行记录的保留天数，其余配置取默认值；配置服务本身在 Sys 的测试中测试</summary>
    private sealed class StubConfigService(int retentionDays) : IConfigService
    {
        public Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default)
            => Task.FromResult(key == CollectConfigKeys.RunRetentionDays ? retentionDays : defaultValue);

        public Task<string?> GetStringAsync(string key, string? defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<IReadOnlyList<ConfigRow>> GetListAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ManageResult> SaveAsync(string key, string value, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ManageResult> ResetAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
