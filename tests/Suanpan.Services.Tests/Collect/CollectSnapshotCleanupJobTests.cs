using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Collect.Entities;
using Suanpan.Services.Collect.Jobs;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Configs.Models;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Mvc;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>快照的清理（采集设计 §4.6，BR-022）：断言只看本测试建的数据</summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CollectSnapshotCleanupJobTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 10, 29, 20, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// 保留天数改为 30：未被引用的快照 30 天后删除，被引用的保留到 365 天；每批 2 条，要删两批。
    /// 内容只在没有快照引用、且 1 天内没有被使用时删除
    /// </summary>
    [Fact]
    public async Task Cleanup_DeletesExpiredSnapshotsAndUnusedContents()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var sourceId = Random.Shared.NextInt64(1_000_000, long.MaxValue);
        var recent = Snapshot(sourceId, Now.AddDays(-29), referenced: false);
        var referenced = Snapshot(sourceId, Now.AddDays(-200), referenced: true);
        db.Snapshots.AddRange(
            recent,
            referenced,
            Snapshot(sourceId, Now.AddDays(-31), referenced: false),
            Snapshot(sourceId, Now.AddDays(-40), referenced: false),
            Snapshot(sourceId, Now.AddDays(-366), referenced: true));
        var orphan = Content(Now.AddDays(-2));
        var justUsed = Content(Now.AddHours(-1));
        db.SnapshotContents.AddRange(orphan, justUsed, Content(Now.AddDays(-2), recent.ContentHash), Content(Now.AddDays(-2), referenced.ContentHash));
        await db.SaveChangesAsync(Ct);
        var time = new FixedTimeProvider(new DateTimeOffset(Now));
        var job = new CollectSnapshotCleanupJob(
            db, new PostgresSnapshotContentStore(db, time), new StubConfigService(retentionDays: 30), new StubDistributedLock(), time,
            NullLogger<CollectSnapshotCleanupJob>.Instance, batchSize: 2);

        await job.RunAsync(Ct);

        Assert.Equal([recent.Id, referenced.Id], await db.Snapshots.Where(s => s.SourceId == sourceId).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(Ct));
        var hashes = new[] { orphan.ContentHash, justUsed.ContentHash, recent.ContentHash, referenced.ContentHash };
        Assert.Equal(
            new[] { justUsed.ContentHash, recent.ContentHash, referenced.ContentHash }.Order(),
            (await db.SnapshotContents.Where(c => hashes.Contains(c.ContentHash)).Select(c => c.ContentHash).ToListAsync(Ct)).Order());
    }

    private static Snapshot Snapshot(long sourceId, DateTime fetchedAt, bool referenced) => new()
    {
        RunId = 1,
        Attempt = 1,
        SourceId = sourceId,
        TaskId = 1,
        Url = "https://example.test/data",
        RequestKey = "https://example.test/data",
        FetchedAt = fetchedAt,
        HttpStatus = 200,
        ContentHash = NewHash(),
        Referenced = referenced,
    };

    private static SnapshotContent Content(DateTime lastUsedAt, string? hash = null)
        => new() { ContentHash = hash ?? NewHash(), Body = [1, 2, 3], LastUsedAt = lastUsedAt };

    private static string NewHash() => (Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"))[..64];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>只给出快照的保留天数，其余配置取默认值；配置服务本身在 Sys 的测试中测试</summary>
    private sealed class StubConfigService(int retentionDays) : IConfigService
    {
        public Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default)
            => Task.FromResult(key == CollectConfigKeys.SnapshotRetentionDays ? retentionDays : defaultValue);

        public Task<string?> GetStringAsync(string key, string? defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default) => Task.FromResult(defaultValue);

        public Task<IReadOnlyList<ConfigRow>> GetListAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ManageResult> SaveAsync(string key, string value, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ManageResult> ResetAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
