using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Common.Auditing;

namespace Suanpan.Core.Collect.Entities;

/// <summary>采集任务（col_task，采集设计 §2.1）= 数据源 + 解析器 + 目标数据类型 + 调度规则；保存时的校验见 CollectTaskRule</summary>
public class CollectTask : AuditedEntity
{
    public long Id { get; set; }

    /// <summary>名称，唯一，如"BLS 发布日程"</summary>
    public string Name { get; set; } = string.Empty;

    public long SourceId { get; set; }

    /// <summary>解析器标识，取自解析器目录（BR-012）</summary>
    public string ParserKey { get; set; } = string.Empty;

    /// <summary>目标数据类型（BR-007），由解析器决定，保存时从目录带出</summary>
    public CollectTargetType TargetType { get; set; }

    /// <summary>5 段 cron，按 UTC 解释；为空表示只能手动触发（PRD FR-002 需求描述）</summary>
    public string? Cron { get; set; }

    /// <summary>解析器参数，JSON 对象（列类型 jsonb）；具体字段由解析器校验</summary>
    public string? Params { get; set; }

    /// <summary>新建时为停用；停用的任务不参与定时调度，但可以手动触发（BR-155）</summary>
    public bool IsEnabled { get; set; }

    /// <summary>调度分发的记账：上一次检查到点的时间（UTC）。修改 cron 或启用任务时重置为当前时间（采集设计 §4.2）</summary>
    public DateTime? LastDispatchedAt { get; set; }

    public string? Remark { get; set; }
}
