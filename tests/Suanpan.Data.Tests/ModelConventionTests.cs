using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Suanpan.Data.PublicViews;
using Xunit;

namespace Suanpan.Data.Tests;

/// <summary>
/// 实体配置约定（docs/conventions/backend.md §6.1 ~ §6.3）：表名、视图名、列名、主键名、索引名都是 snake_case 且不超过
/// PostgreSQL 的 63 字符上限；不写导航属性，不建外键；枚举存 smallint。
/// 随着各模块加入实体，这些测试自动覆盖新的映射。
/// </summary>
public partial class ModelConventionTests
{
    // 仅用于构建模型，不会连接数据库
    private const string DummyConnectionString = "Host=127.0.0.1;Database=model_only";

    private const int MaxIdentifierLength = 63;

    public static TheoryData<string> Contexts => new() { nameof(SuanpanDbContext), nameof(PortalReadDbContext) };

    [Theory]
    [MemberData(nameof(Contexts))]
    public void DatabaseNames_AreSnakeCaseAndWithinLengthLimit(string contextName)
    {
        var violations = DatabaseNames(BuildModel(contextName))
            .Where(n => !SnakeCase().IsMatch(n.Name) || n.Name.Length > MaxIdentifierLength)
            .Select(n => $"{n.Owner}：{n.Name}")
            .ToList();

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

    [Theory]
    [MemberData(nameof(Contexts))]
    public void EnumColumns_AreSmallint(string contextName)
    {
        var violations = BuildModel(contextName).GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType).IsEnum && p.GetColumnType() != "smallint")
            .Select(p => $"{p.DeclaringType.DisplayName()}.{p.Name}：{p.GetColumnType()}")
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void SuanpanDbContext_NamesKeysAndIndexesByConvention()
    {
        var userRole = BuildModel(nameof(SuanpanDbContext)).GetEntityTypes().Single(e => e.GetTableName() == "sys_user_role");

        Assert.Equal("pk_sys_user_role", userRole.FindPrimaryKey()!.GetName());
        Assert.Equal("ix_sys_user_role_role_id", Assert.Single(userRole.GetIndexes()).GetDatabaseName());

        var user = BuildModel(nameof(SuanpanDbContext)).GetEntityTypes().Single(e => e.GetTableName() == "sys_user");
        Assert.Equal("ux_sys_user_username", Assert.Single(user.GetIndexes()).GetDatabaseName());
    }

    [Fact]
    public async Task PortalReadDbContext_RejectsSaving()
    {
        await using var db = new PortalReadDbContext(
            new DbContextOptionsBuilder<PortalReadDbContext>().UseNpgsql(DummyConnectionString).Options);

        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static IEnumerable<(string Owner, string Name)> DatabaseNames(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            var owner = entityType.DisplayName();
            var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
                ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);
            if (storeObject is not { } store)
            {
                continue;
            }

            yield return (owner, store.Name);

            foreach (var property in entityType.GetProperties())
            {
                if (property.GetColumnName(store) is { } column)
                {
                    yield return ($"{owner}.{property.Name}", column);
                }
            }

            if (entityType.FindPrimaryKey()?.GetName() is { } keyName)
            {
                yield return ($"{owner} 主键", keyName);
            }

            foreach (var index in entityType.GetIndexes())
            {
                if (index.GetDatabaseName() is { } indexName)
                {
                    yield return ($"{owner} 索引", indexName);
                }
            }
        }
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
