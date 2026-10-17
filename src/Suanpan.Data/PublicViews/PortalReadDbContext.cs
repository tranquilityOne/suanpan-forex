using Microsoft.EntityFrameworkCore;

namespace Suanpan.Data.PublicViews;

/// <summary>
/// 前台只读上下文（前台账号）：只映射 v_pub_* 视图，查询默认不跟踪，不允许保存（docs/conventions/backend.md §6.8）。
/// 视图的配置放在 PublicViews/ 下，自动扫描。
/// </summary>
public sealed class PortalReadDbContext(DbContextOptions<PortalReadDbContext> options) : DbContext(options)
{
    private const string ConfigurationNamespace = "Suanpan.Data.PublicViews";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PortalReadDbContext).Assembly,
            type => type.Namespace?.StartsWith(ConfigurationNamespace, StringComparison.Ordinal) == true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnly();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw ReadOnly();

    private static InvalidOperationException ReadOnly() => new("前台只读上下文不允许保存数据。");
}
