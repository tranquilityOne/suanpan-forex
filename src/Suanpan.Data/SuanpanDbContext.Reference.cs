using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Reference.Entities;

namespace Suanpan.Data;

/// <summary>基础数据模块：货币、经济体、指标、指标与数据源的映射（基础数据设计 §2）</summary>
public partial class SuanpanDbContext
{
    public DbSet<Currency> Currencies => Set<Currency>();

    public DbSet<Economy> Economies => Set<Economy>();

    public DbSet<Indicator> Indicators => Set<Indicator>();

    public DbSet<IndicatorSource> IndicatorSources => Set<IndicatorSource>();
}
