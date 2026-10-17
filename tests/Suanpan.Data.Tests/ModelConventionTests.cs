using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Suanpan.Data.PublicViews;
using Xunit;

namespace Suanpan.Data.Tests;

/// <summary>
/// 实体配置约定（docs/conventions/backend.md §6.1）：表名、视图名、列名都是 snake_case；不写导航属性，不建外键。
/// 随着各模块加入实体，这些测试自动覆盖新的映射。
/// </summary>
public partial class ModelConventionTests
{
    // 仅用于构建模型，不会连接数据库
    private const string DummyConnectionString = "Host=127.0.0.1;Database=model_only";

    public static TheoryData<string> Contexts => new() { nameof(SuanpanDbContext), nameof(PortalReadDbContext) };

    [Theory]
    [MemberData(nameof(Contexts))]
    public void TableAndColumnNames_AreSnakeCase(string contextName)
    {
        var violations = new List<string>();

        foreach (var entityType in BuildModel(contextName).GetEntityTypes())
        {
            var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
                ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
            if (storeObject is not { } store)
            {
                continue;
            }

            if (!SnakeCase().IsMatch(store.Name))
            {
                violations.Add($"{entityType.DisplayName()}：{store.Name}");
            }

            foreach (var property in entityType.GetProperties())
            {
                var column = property.GetColumnName(store);
                if (column is not null && !SnakeCase().IsMatch(column))
                {
                    violations.Add($"{entityType.DisplayName()}.{property.Name}：{column}");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Entities_HaveNoNavigationsOrForeignKeys(string contextName)
    {
        var violations = BuildModel(contextName).GetEntityTypes()
            .Where(e => e.GetNavigations().Any() || e.GetSkipNavigations().Any() || e.GetForeignKeys().Any())
            .Select(e => e.DisplayName())
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public async Task PortalReadDbContext_RejectsSaving()
    {
        await using var db = new PortalReadDbContext(
            new DbContextOptionsBuilder<PortalReadDbContext>().UseNpgsql(DummyConnectionString).Options);

        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static IModel BuildModel(string contextName)
    {
        DbContext db = contextName switch
        {
            nameof(SuanpanDbContext) => new SuanpanDbContext(
                new DbContextOptionsBuilder<SuanpanDbContext>().UseNpgsql(DummyConnectionString).Options),
            nameof(PortalReadDbContext) => new PortalReadDbContext(
                new DbContextOptionsBuilder<PortalReadDbContext>().UseNpgsql(DummyConnectionString).Options),
            _ => throw new ArgumentOutOfRangeException(nameof(contextName)),
        };

        using (db)
        {
            return db.GetService<IDesignTimeModel>().Model;
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex SnakeCase();
}
