using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Common.Localization;

namespace Suanpan.Data.Configurations.Common;

/// <summary>中英文文本的列映射（后端约定 §6.3）：一个 <see cref="BilingualText"/> 属性映射成 {前缀}_zh、{前缀}_en 两列。</summary>
public static class BilingualColumnExtensions
{
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
}
