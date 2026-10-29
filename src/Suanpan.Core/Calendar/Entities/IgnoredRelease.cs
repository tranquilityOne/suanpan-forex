using Suanpan.Core.Common.Auditing;

namespace Suanpan.Core.Calendar.Entities;

/// <summary>
/// 已忽略的发布（cal_ignored_release，BR-156，日历设计 §4.5）：按数据源 + 发布标识忽略，以后同一标识的条目不再写入未匹配条目。
/// 带创建两件套，记录谁、什么时候忽略的；恢复即删除这一行
/// </summary>
public class IgnoredRelease : CreationAuditedEntity
{
    public long Id { get; set; }

    public long SourceId { get; set; }

    public string ReleaseKey { get; set; } = string.Empty;

    public string ReleaseName { get; set; } = string.Empty;
}
