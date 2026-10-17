using Microsoft.EntityFrameworkCore;

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
    }
}
