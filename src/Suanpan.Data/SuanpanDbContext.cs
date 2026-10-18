using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Suanpan.Data;

/// <summary>
/// 业务库上下文（应用账号）。各模块的 DbSet 写在 SuanpanDbContext.{模块}.cs 中，
/// 实体配置放在 Configurations/{模块}/，自动扫描（docs/conventions/backend.md §6.1）。
/// </summary>
public partial class SuanpanDbContext(DbContextOptions<SuanpanDbContext> options) : DbContext(options)
{
    private const string ConfigurationNamespace = "Suanpan.Data.Configurations";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 只扫描 Configurations 下的配置，对外视图的配置归 PortalReadDbContext
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SuanpanDbContext).Assembly,
            type => type.Namespace?.StartsWith(ConfigurationNamespace, StringComparison.Ordinal) == true);

        ApplyConstraintNames(modelBuilder.Model);
    }

    /// <summary>
    /// 把多次保存放进同一个事务：全部成功才提交。外层已经开启事务时直接加入外层事务（如测试中每个用例的事务）。
    /// </summary>
    public async Task InTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is not null)
        {
            await action();
            return;
        }

        await using var transaction = await Database.BeginTransactionAsync(ct);
        await action();
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// 主键命名为 pk_{表}，唯一索引为 ux_{表}_{列}，普通索引为 ix_{表}_{列}，全部小写，
    /// 在 PostgreSQL 中引用时不用加引号（docs/conventions/backend.md §6.2）。实体配置中显式指定的名字保持不变。
    /// </summary>
    private static void ApplyConstraintNames(IMutableModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            var table = entityType.GetTableName();
            if (table is null)
            {
                continue;
            }

            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is not null && primaryKey.FindAnnotation(RelationalAnnotationNames.Name) is null)
            {
                primaryKey.SetName($"pk_{table}");
            }

            foreach (var index in entityType.GetIndexes())
            {
                if (index.FindAnnotation(RelationalAnnotationNames.Name) is not null)
                {
                    continue;
                }

                var columns = string.Join("_", index.Properties.Select(p => p.GetColumnName()));
                index.SetDatabaseName($"{(index.IsUnique ? "ux" : "ix")}_{table}_{columns}");
            }
        }
    }
}
