using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Common.Auditing;

namespace Suanpan.Data.Configurations.Common;

/// <summary>审计字段的列映射，在各实体配置中调用。</summary>
public static class AuditColumnExtensions
{
    /// <summary>映射 created_by、created_at。</summary>
    public static EntityTypeBuilder<T> HasCreationAuditColumns<T>(this EntityTypeBuilder<T> builder)
        where T : CreationAuditedEntity
    {
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(64).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        return builder;
    }

    /// <summary>映射审计四件套：created_by、created_at、updated_by、updated_at。</summary>
    public static EntityTypeBuilder<T> HasAuditColumns<T>(this EntityTypeBuilder<T> builder)
        where T : AuditedEntity
    {
        builder.HasCreationAuditColumns();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        return builder;
    }
}
