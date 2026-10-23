using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Common.Localization;

namespace Suanpan.Data.Configurations.Common;

/// <summary>中英文文本的列映射（后端约定 §6.3）：一个 <see cref="BilingualText"/> 属性映射成 {前缀}_zh、{前缀}_en 两列。</summary>
public static class BilingualColumnExtensions
{
    /// <summary>必填的文本（如名称）：两列都 NOT NULL</summary>
    public static EntityTypeBuilder<T> HasBilingualColumns<T>(
        this EntityTypeBuilder<T> builder,
        Expression<Func<T, BilingualText?>> property,
        string columnPrefix,
        int zhMaxLength,
        int enMaxLength)
        where T : class
    {
        builder.ComplexProperty(property, text =>
        {
            text.IsRequired();
            text.Property(t => t.Zh).HasColumnName($"{columnPrefix}_zh").HasMaxLength(zhMaxLength).IsRequired();
            text.Property(t => t.En).HasColumnName($"{columnPrefix}_en").HasMaxLength(enMaxLength).IsRequired();
        });
        return builder;
    }

    /// <summary>
    /// 可空的文本（如说明）：属性为 null 时两列都为 NULL；列类型为 text，不限长度。
    /// 只填了一种语言时，另一种存空串（基础数据设计 §3.7）
    /// </summary>
    public static EntityTypeBuilder<T> HasOptionalBilingualColumns<T>(
        this EntityTypeBuilder<T> builder,
        Expression<Func<T, BilingualText?>> property,
        string columnPrefix)
        where T : class
    {
        builder.ComplexProperty(property, text =>
        {
            text.IsRequired(false);
            text.Property(t => t.Zh).HasColumnName($"{columnPrefix}_zh");
            text.Property(t => t.En).HasColumnName($"{columnPrefix}_en");
        });
        return builder;
    }
}
