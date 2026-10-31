using System.Text;
using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
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

        var ids = new[] { first[0], second[0], second[1] };
        var saved = await db.Snapshots.Where(s => ids.Contains(s.Id)).OrderBy(s => s.Id).ToListAsync(Ct);
        Assert.Equal([false, true, false], saved.Select(s => s.Unchanged));
        Assert.Equal([(short)0, (short)0, (short)1], saved.Select(s => s.Seq));
        Assert.Single(saved.Select(s => s.ContentHash).Distinct());
        Assert.Equal(1, await db.SnapshotContents.CountAsync(c => c.ContentHash == saved[0].ContentHash, Ct));
        Assert.Equal(body, await new PostgresSnapshotContentStore(db, new FixedTimeProvider(FetchedAt)).GetAsync(saved[0].ContentHash, Ct));
    }

    /// <summary>盯盘只保存内容有变化的快照（BR-016）：未变化的响应不写快照记录，对应位置返回上一个相同内容的快照 id</summary>
    [Fact]
    public async Task Save_SkipUnchanged_WritesNoRecordForUnchangedResponse()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Service(db);
        var key = TestData.Unique("key");
        var body = Encoding.UTF8.GetBytes($"内容 {Guid.NewGuid()}");
        var first = await service.SaveAsync(Batch(1, (key, body)), skipUnchanged: true, Ct);

        var again = await service.SaveAsync(Batch(2, (key, body)), skipUnchanged: true, Ct);
        var changed = await service.SaveAsync(Batch(3, (key, Encoding.UTF8.GetBytes($"新的内容 {Guid.NewGuid()}"))), skipUnchanged: true, Ct);

        Assert.Equal(Assert.Single(first), Assert.Single(again));
        Assert.NotEqual(Assert.Single(first), Assert.Single(changed));
        Assert.Equal(2, await db.Snapshots.CountAsync(s => s.SourceId == SourceId && s.RequestKey == key, Ct));
    }

    /// <summary>
    /// FR-004 主流程第 1、2 步：按任务、所属运行记录的状态、是否有效筛选，按请求时间倒序，带数据源和任务的名称；
    /// 详情带原始内容和所属运行记录
    /// </summary>
    [Fact]
    public async Task Query_FiltersByTaskRunStatusAndValidity()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Service(db);
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id);
        var succeeded = await AddRunAsync(db, task, CollectRunStatus.Succeeded);
        var failed = await AddRunAsync(db, task, CollectRunStatus.Failed);
        var body = Encoding.UTF8.GetBytes($"内容 {Guid.NewGuid()}");
        var first = await service.SaveAsync(TaskBatch(succeeded, task, body, body), skipUnchanged: false, Ct);
        var second = await service.SaveAsync(TaskBatch(failed, task, body), skipUnchanged: false, Ct);
        await service.MarkInvalidAsync([first[1]], "拦截页", Ct);

        var all = (await service.QueryAsync(new SnapshotQuery(TaskId: task.Id), Ct)).Items;
        var ofFailedRuns = (await service.QueryAsync(new SnapshotQuery(TaskId: task.Id, RunStatus: CollectRunStatus.Failed), Ct)).Items;
        var invalid = (await service.QueryAsync(new SnapshotQuery(TaskId: task.Id, IsValid: false), Ct)).Items;
        var detail = await service.GetAsync(first[0], Ct);

        Assert.Equal([second[0], first[1], first[0]], all.Select(r => (long?)r.Id));
        Assert.Equal((source.Name, task.Name, (CollectRunStatus?)CollectRunStatus.Failed), (all[0].SourceName, all[0].TaskName, all[0].RunStatus));
        Assert.Equal([second[0]], ofFailedRuns.Select(r => r.Id));
        Assert.Equal([first[1]], invalid.Select(r => r.Id));
        Assert.Equal(body, detail!.Content);
        Assert.Equal(succeeded.Id, detail.Run!.Id);
    }

    private static async Task<CollectRun> AddRunAsync(SuanpanDbContext db, CollectTask task, CollectRunStatus status)
    {
        var run = new CollectRun { TaskId = task.Id, SourceId = task.SourceId, Trigger = CollectRunTrigger.Manual, Status = status, QueuedAt = FetchedAt };
        db.CollectRuns.Add(run);
        await db.SaveChangesAsync(Ct);
        return run;
    }

    /// <summary>运行记录第 1 次请求的响应，每个响应一个随机的请求标识</summary>
    private static SnapshotBatch TaskBatch(CollectRun run, CollectTask task, params byte[][] bodies) => new(
        run.Id, 1, task.Id, task.SourceId,
        bodies.Select((body, i) => new SnapshotInput($"https://example.test/{run.Id}/{i}", TestData.Unique("key"), 200, "text/plain", body, FetchedAt)).ToList());

    private static SnapshotService Service(SuanpanDbContext db) => new(db, new PostgresSnapshotContentStore(db, new FixedTimeProvider(FetchedAt)));

    private static SnapshotBatch Batch(long runId, params (string Key, byte[] Body)[] responses) => new(
        runId, 1, TaskId: 900001, SourceId,
        responses.Select(r => new SnapshotInput($"https://example.test/{r.Key}", r.Key, 200, "text/plain; charset=utf-8", r.Body, FetchedAt)).ToList());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
