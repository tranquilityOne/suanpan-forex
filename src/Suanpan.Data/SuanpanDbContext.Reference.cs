using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Reference.Entities;

namespace Suanpan.Data;

/// <summary>基础数据模块：货币、经济体、指标（基础数据设计 §2）</summary>
public partial class SuanpanDbContext
{
    public DbSet<Currency> Currencies => Set<Currency>();

    public DbSet<Economy> Economies => Set<Economy>();
}
