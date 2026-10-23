using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Rules;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Reference;

public class IndicatorConfiguration : IEntityTypeConfiguration<Indicator>
{
    public void Configure(EntityTypeBuilder<Indicator> builder)
    {
        builder.ToTable("ref_indicator");

        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(ReferenceCodeRule.MaxIndicatorCodeLength);
        builder.Property(x => x.EconomyCode).HasColumnName("economy_code").HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.HasBilingualColumns(x => x.Name, "name", zhMaxLength: 64, enMaxLength: 128);
        builder.HasBilingualColumns(x => x.ShortName, "short_name", zhMaxLength: 32, enMaxLength: 64);
        builder.Property(x => x.Category).HasColumnName("category").HasConversion<short>();
        builder.Property(x => x.ValueType).HasColumnName("value_type").HasConversion<short>();
        builder.Property(x => x.Unit).HasColumnName("unit").HasConversion<short>();
        builder.Property(x => x.Decimals).HasColumnName("decimals");
        builder.Property(x => x.Frequency).HasColumnName("frequency").HasConversion<short>();
        builder.Property(x => x.Importance).HasColumnName("importance").HasConversion<short>();
        builder.Property(x => x.Direction).HasColumnName("direction").HasConversion<short>();
        builder.Property(x => x.FlatTolerance).HasColumnName("flat_tolerance").HasPrecision(18, 6);
        builder.Property(x => x.RangeMin).HasColumnName("range_min").HasPrecision(18, 6);
        builder.Property(x => x.RangeMax).HasColumnName("range_max").HasPrecision(18, 6);
        builder.Property(x => x.SigmaN).HasColumnName("sigma_n").HasPrecision(18, 6);
        builder.HasBilingualColumns(x => x.Agency, "agency", zhMaxLength: 64, enMaxLength: 128);
        builder.Property(x => x.DefaultReleaseTime).HasColumnName("default_release_time");
        builder.HasOptionalBilingualColumns(x => x.Description, "description");
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.HasAuditColumns();

        // 按经济体筛选；删除经济体、货币前检查引用
        builder.HasIndex(x => x.EconomyCode);
        builder.HasIndex(x => x.CurrencyCode);
    }
}
