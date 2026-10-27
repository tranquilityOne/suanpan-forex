using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Data;
using Suanpan.Services.Collect.Tasks;
using Suanpan.Services.Collect.Tasks.Models;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>
/// 采集任务的保存、启停、删除、列表（采集设计 §3.2、§4.2，FR-002）。字段规则和 cron 写法的各个分支在
/// CollectTaskRuleTests、CronScheduleTests 中测试，这里测服务接上了它们，以及需要查库的校验。
/// 解析器目录在 M1 为空，用替身提供两个解析器
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CollectTaskServiceTests(PostgresFixture postgres)
{
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

    /// <summary>FR-002 主流程第 1 步、BR-012：新建为停用，目标数据类型由解析器决定，cron 统一写法</summary>
    [Fact]
    public async Task Create_SavesDisabledTaskWithParserTargetType()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var name = TestData.Unique("任务");

        var result = await Create(db).CreateAsync(Input(name, source.Id) with { Cron = " 0  1 * * * " }, Ct);

        Assert.True(result.Success, result.Error);
        var saved = await db.CollectTasks.AsNoTracking().SingleAsync(t => t.Name == name, Ct);
        Assert.Equal((false, CollectTargetType.Calendar, "0 1 * * *"), (saved.IsEnabled, saved.TargetType, saved.Cron));
    }

    /// <summary>cron 写法错误（ERR-COL-001）、名称重复、数据源不存在、解析器不在目录中，都在对应的字段提示</summary>
    [Fact]
    public async Task Create_ReportsInvalidFields()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var name = TestData.Unique("任务");
        await service.CreateAsync(Input(name, source.Id), Ct);

        var withSeconds = await service.CreateAsync(Input(TestData.Unique("任务"), source.Id) with { Cron = "*/10 * * * * *" }, Ct);
        var duplicate = await service.CreateAsync(Input(name, source.Id), Ct);
        var noSource = await service.CreateAsync(Input(TestData.Unique("任务"), -1), Ct);
        var unknownParser = await service.CreateAsync(Input(TestData.Unique("任务"), source.Id) with { ParserKey = "us.test.unknown" }, Ct);

        Assert.Equal((false, nameof(CollectTaskInput.Cron)), (withSeconds.Success, withSeconds.Field));
        Assert.Equal((false, nameof(CollectTaskInput.Name)), (duplicate.Success, duplicate.Field));
        Assert.Equal((false, nameof(CollectTaskInput.SourceId)), (noSource.Success, noSource.Field));
        Assert.Equal((false, nameof(CollectTaskInput.ParserKey)), (unknownParser.Success, unknownParser.Field));
    }

    /// <summary>FR-002 验收第 1 条：修改 cron 后从现在起按新规则算（重置调度的记账时间）；cron 没变时不动它</summary>
    [Fact]
    public async Task Update_ResetsDispatchClockOnlyWhenCronChanges()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id, enabled: true, cron: "0 1 * * *");
        var earlier = _clock.UtcNow.UtcDateTime.AddHours(-3);
        await db.CollectTasks.Where(t => t.Id == task.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.LastDispatchedAt, earlier), Ct);
        db.ChangeTracker.Clear();

        await service.UpdateAsync(task.Id, Input(task.Name, source.Id) with { Cron = "0 1 * * *", Remark = "只改备注" }, Ct);
        var unchanged = await DispatchedAtAsync(db, task.Id);
        await service.UpdateAsync(task.Id, Input(task.Name, source.Id) with { Cron = "30 1 * * *" }, Ct);

        Assert.Equal(earlier, unchanged);
        Assert.Equal(_clock.UtcNow.UtcDateTime, await DispatchedAtAsync(db, task.Id));
    }

    /// <summary>PRD FR-002 前置条件：数据源停用时任务不能启用；启用时重置调度的记账时间</summary>
    [Fact]
    public async Task SetEnabled_RequiresEnabledSource()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var disabledSource = await TestData.AddDataSourceAsync(db, Ct, enabled: false);
        var enabledSource = await TestData.AddDataSourceAsync(db, Ct);
        var blocked = await TestData.AddCollectTaskAsync(db, Ct, disabledSource.Id);
        var task = await TestData.AddCollectTaskAsync(db, Ct, enabledSource.Id);

        var rejected = await service.SetEnabledAsync(blocked.Id, true, Ct);
        var accepted = await service.SetEnabledAsync(task.Id, true, Ct);

        Assert.False(rejected.Success);
        Assert.True(accepted.Success, accepted.Error);
        var saved = await db.CollectTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id, Ct);
        Assert.Equal((true, _clock.UtcNow.UtcDateTime), (saved.IsEnabled, saved.LastDispatchedAt));
    }

    /// <summary>BR-154：有运行记录的任务不能删除；没有的可以</summary>
    [Fact]
    public async Task Delete_OnlyTasksWithoutRuns()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var used = await TestData.AddCollectTaskAsync(db, Ct, source.Id);
        var unused = await TestData.AddCollectTaskAsync(db, Ct, source.Id);
        db.CollectRuns.Add(Run(used, CollectRunStatus.Succeeded, _clock.UtcNow.UtcDateTime));
        await db.SaveChangesAsync(Ct);

        var rejected = await service.DeleteAsync(used.Id, Ct);
        var deleted = await service.DeleteAsync(unused.Id, Ct);

        Assert.False(rejected.Success);
        Assert.True(deleted.Success, deleted.Error);
        Assert.Equal([used.Id], await db.CollectTasks.Where(t => t.SourceId == source.Id).Select(t => t.Id).ToListAsync(Ct));
    }

    /// <summary>列表带最近一次运行的状态、解析器名称和下次执行时间（采集设计 §8.1）；停用的任务没有下次执行时间</summary>
    [Fact]
    public async Task Query_ShowsLastRunAndNextRunTime()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id, enabled: true, cron: "0 1 * * *");
        var paused = await TestData.AddCollectTaskAsync(db, Ct, source.Id, cron: "0 1 * * *");
        var now = _clock.UtcNow.UtcDateTime;
        db.CollectRuns.AddRange(Run(task, CollectRunStatus.Failed, now.AddHours(-2)), Run(task, CollectRunStatus.Succeeded, now.AddHours(-1)));
        await db.SaveChangesAsync(Ct);

        var rows = (await Create(db).QueryAsync(new CollectTaskQuery(SourceId: source.Id), Ct)).Items.ToDictionary(r => r.Id);

        Assert.Equal((CollectRunStatus.Succeeded, now.AddHours(-1)), (rows[task.Id].LastRunStatus, rows[task.Id].LastRunAt));
        Assert.Equal((StubParserCatalog.Schedule.Name, new DateTime(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc)), (rows[task.Id].ParserName, rows[task.Id].NextRunAt));
        Assert.Equal((null, null), (rows[paused.Id].LastRunStatus, rows[paused.Id].NextRunAt));
    }

    private CollectTaskService Create(SuanpanDbContext db) => new(db, new StubParserCatalog(), _clock);

    private static CollectTaskInput Input(string name, long sourceId) => new()
    {
        Name = name,
        SourceId = sourceId,
        ParserKey = StubParserCatalog.Schedule.Key,
        Cron = "0 1 * * *",
        Params = "{\"series\": [\"CES0000000001\"]}",
    };

    private static CollectRun Run(CollectTask task, CollectRunStatus status, DateTime queuedAt) => new()
    {
        TaskId = task.Id,
        SourceId = task.SourceId,
        Trigger = CollectRunTrigger.Manual,
        Status = status,
        QueuedAt = queuedAt,
    };

    private static Task<DateTime?> DispatchedAtAsync(SuanpanDbContext db, long taskId)
        => db.CollectTasks.AsNoTracking().Where(t => t.Id == taskId).Select(t => t.LastDispatchedAt).SingleAsync(Ct);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
