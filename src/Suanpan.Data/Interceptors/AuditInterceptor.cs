using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Suanpan.Core.Common.Auditing;

namespace Suanpan.Data.Interceptors;

/// <summary>
/// 保存时自动填充审计字段：新增时填创建字段，修改时只填修改字段，创建字段保持不变。
/// <c>ExecuteUpdateAsync</c>、<c>ExecuteSqlAsync</c> 不经过本拦截器，需要自行写修改字段（docs/conventions/backend.md §6.1）。
/// </summary>
public sealed class AuditInterceptor(IAuditActorProvider actorProvider, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var actor = actorProvider.GetActor();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in context.ChangeTracker.Entries<CreationAuditedEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedBy).CurrentValue = actor;
                    entry.Property(e => e.CreatedAt).CurrentValue = now;
                    break;

                case EntityState.Modified:
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    if (entry.Entity is AuditedEntity)
                    {
                        entry.Property(nameof(AuditedEntity.UpdatedBy)).CurrentValue = actor;
                        entry.Property(nameof(AuditedEntity.UpdatedAt)).CurrentValue = now;
                    }
                    break;
            }
        }
    }
}
