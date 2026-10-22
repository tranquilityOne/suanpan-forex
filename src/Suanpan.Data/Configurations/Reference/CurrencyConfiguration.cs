using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Reference.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Reference;

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("ref_currency");

        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(3).IsFixedLength();
        builder.HasBilingualColumns(x => x.Name, "name", zhMaxLength: 32, enMaxLength: 64);
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.HasAuditColumns();

        // 预置货币在迁移中用 InsertData 写入：HasData 会丢掉复合类型（中英文文本）的值（基础数据设计 §2.4）
    }
}
