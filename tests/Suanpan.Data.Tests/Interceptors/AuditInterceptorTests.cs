using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Common.Auditing;
using Suanpan.Data.Interceptors;
using Xunit;

namespace Suanpan.Data.Tests.Interceptors;

public class AuditInterceptorTests
{
    private static readonly DateTimeOffset CreateTime = new(2026, 10, 17, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdateTime = new(2026, 10, 17, 2, 30, 0, TimeSpan.Zero);

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly SettableTimeProvider _time = new() { UtcNow = CreateTime };
    private readonly SettableActorProvider _actor = new() { Actor = "zhangsan" };

    [Fact]
    public async Task Added_FillsCreationFields_AndLeavesUpdateFieldsEmpty()
    {
        await using (var db = CreateContext())
        {
            db.Notes.Add(new Note { Id = 1, Text = "a" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var saved = await LoadNoteAsync(1);
        Assert.Equal("zhangsan", saved.CreatedBy);
        Assert.Equal(CreateTime.UtcDateTime, saved.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
        Assert.Null(saved.UpdatedBy);
        Assert.Null(saved.UpdatedAt);
    }

    [Fact]
    public async Task Modified_FillsUpdateFields_AndKeepsCreationFields()
    {
        await AddNoteAsync(1);
        _time.UtcNow = UpdateTime;
        _actor.Actor = "lisi";

        await using (var db = CreateContext())
        {
            var note = await db.Notes.SingleAsync(n => n.Id == 1, TestContext.Current.CancellationToken);
            note.Text = "b";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var saved = await LoadNoteAsync(1);
        Assert.Equal("b", saved.Text);
        Assert.Equal("zhangsan", saved.CreatedBy);
        Assert.Equal(CreateTime.UtcDateTime, saved.CreatedAt);
        Assert.Equal("lisi", saved.UpdatedBy);
        Assert.Equal(UpdateTime.UtcDateTime, saved.UpdatedAt);
    }

    [Fact]
    public async Task Modified_DoesNotSaveChangesToCreationFields()
    {
        await AddNoteAsync(1);
        _time.UtcNow = UpdateTime;

        await using (var db = CreateContext())
        {
            var note = await db.Notes.SingleAsync(n => n.Id == 1, TestContext.Current.CancellationToken);
            db.Entry(note).Property(n => n.CreatedBy).CurrentValue = "someone-else";
            db.Entry(note).Property(n => n.CreatedAt).CurrentValue = UpdateTime.UtcDateTime;
            note.Text = "b";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var saved = await LoadNoteAsync(1);
        Assert.Equal("zhangsan", saved.CreatedBy);
        Assert.Equal(CreateTime.UtcDateTime, saved.CreatedAt);
    }

    [Fact]
    public async Task Unchanged_DoesNotFillUpdateFields()
    {
        await AddNoteAsync(1);
        _time.UtcNow = UpdateTime;

        await using (var db = CreateContext())
        {
            _ = await db.Notes.SingleAsync(n => n.Id == 1, TestContext.Current.CancellationToken);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var saved = await LoadNoteAsync(1);
        Assert.Null(saved.UpdatedBy);
        Assert.Null(saved.UpdatedAt);
    }

    [Fact]
    public async Task CreationOnlyEntity_Added_FillsCreationFields()
    {
        _actor.Actor = "collector";

        await using (var db = CreateContext())
        {
            db.Tags.Add(new Tag { Id = 1, Name = "t" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = CreateContext();
        var saved = await check.Tags.SingleAsync(t => t.Id == 1, TestContext.Current.CancellationToken);
        Assert.Equal("collector", saved.CreatedBy);
        Assert.Equal(CreateTime.UtcDateTime, saved.CreatedAt);
    }

    [Fact]
    public void SyncSaveChanges_AlsoFillsFields()
    {
        using (var db = CreateContext())
        {
            db.Notes.Add(new Note { Id = 1, Text = "a" });
            db.SaveChanges();
        }

        using var check = CreateContext();
        var saved = check.Notes.Single(n => n.Id == 1);
        Assert.Equal("zhangsan", saved.CreatedBy);
        Assert.Equal(CreateTime.UtcDateTime, saved.CreatedAt);
    }

    private async Task AddNoteAsync(long id)
    {
        await using var db = CreateContext();
        db.Notes.Add(new Note { Id = id, Text = "a" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Note> LoadNoteAsync(long id)
    {
        await using var db = CreateContext();
        return await db.Notes.AsNoTracking().SingleAsync(n => n.Id == id, TestContext.Current.CancellationToken);
    }

    private TestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .AddInterceptors(new AuditInterceptor(_actor, _time))
            .Options);

    public sealed class Note : AuditedEntity
    {
        public long Id { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class Tag : CreationAuditedEntity
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<Tag> Tags => Set<Tag>();
    }

    private sealed class SettableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class SettableActorProvider : IAuditActorProvider
    {
        public string Actor { get; set; } = string.Empty;

        public string GetActor() => Actor;
    }
}
