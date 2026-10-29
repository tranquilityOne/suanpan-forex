using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Collect.Entities;

namespace Suanpan.Data;

/// <summary>数据采集模块：数据源、采集任务、运行记录、快照（采集设计 §2）</summary>
public partial class SuanpanDbContext
{
    public DbSet<DataSource> DataSources => Set<DataSource>();

    public DbSet<CollectTask> CollectTasks => Set<CollectTask>();

    public DbSet<CollectRun> CollectRuns => Set<CollectRun>();

    public DbSet<Snapshot> Snapshots => Set<Snapshot>();

    public DbSet<SnapshotContent> SnapshotContents => Set<SnapshotContent>();
}
