using Suanpan.Core.Collect.Enums;

namespace Suanpan.Crm.Models.Collect;

/// <summary>"立即执行"按钮（采集设计 §8.2），任务列表和运行详情共用</summary>
public sealed record CollectTriggerButtonModel(long TaskId, string TaskName, CollectTargetType TargetType, string ButtonClass = "btn btn-sm btn-outline-primary");
