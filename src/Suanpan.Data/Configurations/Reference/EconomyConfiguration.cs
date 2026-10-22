using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Reference.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Reference;

public class EconomyConfiguration : IEntityTypeConfiguration<Economy>
{
    public void Configure(EntityTypeBuilder<Economy> builder)
    {
        builder.ToTable("ref_economy");

        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(2).IsFixedLength();
        builder.HasBilingualColumns(x => x.Name, "name", zhMaxLength: 32, enMaxLength: 64);
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.TimeZone).HasColumnName("timezone").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Sort).HasColumnName("sort");
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.HasAuditColumns();

        // 删除货币前检查引用
        builder.HasIndex(x => x.CurrencyCode);

        // 预置经济体在迁移中用 InsertData 写入：HasData 会丢掉复合类型（中英文文本）的值（基础数据设计 §2.4）
    }
}
