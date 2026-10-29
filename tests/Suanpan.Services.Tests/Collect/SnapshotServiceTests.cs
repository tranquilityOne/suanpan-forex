using System.Text;
using Microsoft.EntityFrameworkCore;
using Suanpan.Data;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Collect.Snapshots.Models;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>快照的保存（采集设计 §5.5，BR-021、FR-004 验收第 2 条）：内容按哈希只存一份，"未变化"按同一数据源、同一请求标识判断</summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class SnapshotServiceTests(PostgresFixture postgres)
{
    /// <summary>快照记录不建外键，用一个固定的数据源 id；各测试用随机的请求标识和内容区分</summary>
    private const long SourceId = 900001;

    private static readonly DateTime FetchedAt = new(2026, 10, 29, 8, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// 同一请求标识的内容没变时记为"未变化"，内容表只有一份；另一个请求标识即使内容相同也不算"未变化"，但共用同一份内容。
    /// 取回的内容与原始字节相同（压缩后存储）
    /// </summary>
    [Fact]
    public async Task Save_StoresContentOnceAndMarksUnchanged()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Service(db);
        var key = TestData.Unique("key");
        var body = Encoding.UTF8.GetBytes($"内容 {Guid.NewGuid()}");

        var first = await service.SaveAsync(Batch(1, (key, body)), skipUnchanged: false, Ct);
        var second = await service.SaveAsync(Batch(2, (key, body), (key + "_other", body)), skipUnchanged: false, Ct);

        var ids = new[] { first[0]!.Value, second[0]!.Value, second[1]!.Value };
        var saved = await db.Snapshots.Where(s => ids.Contains(s.Id)).OrderBy(s => s.Id).ToListAsync(Ct);
        Assert.Equal([false, true, false], saved.Select(s => s.Unchanged));
        Assert.Equal([(short)0, (short)0, (short)1], saved.Select(s => s.Seq));
        Assert.Single(saved.Select(s => s.ContentHash).Distinct());
        Assert.Equal(1, await db.SnapshotContents.CountAsync(c => c.ContentHash == saved[0].ContentHash, Ct));
        Assert.Equal(body, await new PostgresSnapshotContentStore(db, new FixedTimeProvider(FetchedAt)).GetAsync(saved[0].ContentHash, Ct));
    }

    /// <summary>盯盘只保存内容有变化的快照（BR-016）：未变化的响应不写快照记录，对应位置返回 null</summary>
    [Fact]
    public async Task Save_SkipUnchanged_WritesNoRecordForUnchangedResponse()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Service(db);
        var key = TestData.Unique("key");
        var body = Encoding.UTF8.GetBytes($"内容 {Guid.NewGuid()}");
        await service.SaveAsync(Batch(1, (key, body)), skipUnchanged: true, Ct);

        var again = await service.SaveAsync(Batch(2, (key, body)), skipUnchanged: true, Ct);
        var changed = await service.SaveAsync(Batch(3, (key, Encoding.UTF8.GetBytes($"新的内容 {Guid.NewGuid()}"))), skipUnchanged: true, Ct);

        Assert.Null(Assert.Single(again));
        Assert.NotNull(Assert.Single(changed));
        Assert.Equal(2, await db.Snapshots.CountAsync(s => s.SourceId == SourceId && s.RequestKey == key, Ct));
    }

    private static SnapshotService Service(SuanpanDbContext db) => new(db, new PostgresSnapshotContentStore(db, new FixedTimeProvider(FetchedAt)));

    private static SnapshotBatch Batch(long runId, params (string Key, byte[] Body)[] responses) => new(
        runId, 1, TaskId: 900001, SourceId,
        responses.Select(r => new SnapshotInput($"https://example.test/{r.Key}", r.Key, 200, "text/plain; charset=utf-8", r.Body, FetchedAt)).ToList());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
