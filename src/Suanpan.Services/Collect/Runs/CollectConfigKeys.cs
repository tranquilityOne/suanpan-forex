using System.Globalization;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Configs.Models;

namespace Suanpan.Services.Collect.Runs;

/// <summary>
/// 数据采集的配置键和默认值（采集设计 §10），读取方式同 SysConfigKeys：
/// <c>config.GetIntAsync(CollectConfigKeys.RunRetentionDays, CollectConfigKeys.RunRetentionDaysDefault)</c>。
/// 新增配置项时同时加入 <see cref="Definitions"/>（由 ConfigDefinitions 汇总），系统配置页面才能显示和修改。
/// </summary>
public static class CollectConfigKeys
{
    private const string CollectGroup = "数据采集";

    /// <summary>请求失败后的重试间隔（秒），逗号分隔，个数即最多重试次数（BR-009）；格式不对时按默认值</summary>
    public const string RetryDelaySeconds = "collect:retry_delay_seconds";
    public const string RetryDelaySecondsDefault = RetryPolicy.DefaultDelaySeconds;

    /// <summary>运行记录保留天数（BR-011）</summary>
    public const string RunRetentionDays = "collect:run_retention_days";
    public const int RunRetentionDaysDefault = 90;

    /// <summary>快照保留天数（BR-022）；被入库数据引用的快照保留 365 天和它之中较长者</summary>
    public const string SnapshotRetentionDays = "collect:snapshot_retention_days";
    public const int SnapshotRetentionDaysDefault = 180;

    /// <summary>盯盘总开关（PRD §13.1.5 watch_enabled，发布控制 FR-024 实现前的过渡，采集设计 §5.7）</summary>
    public const string WatchEnabled = "collect:watch_enabled";
    public const bool WatchEnabledDefault = true;

    /// <summary>盯盘是否包括中重要性的事件（PRD FR-003 需求描述）</summary>
    public const string WatchIncludeMedium = "collect:watch_include_medium";
    public const bool WatchIncludeMediumDefault = false;

    /// <summary>盯盘提前秒数的默认值（BR-013）；"公布值"映射上可按指标配置</summary>
    public const string WatchLeadSeconds = "collect:watch_lead_seconds";

    /// <summary>盯盘窗口（排期后多少分钟结束）的默认值（BR-013）</summary>
    public const string WatchWindowMinutes = "collect:watch_window_minutes";

    /// <summary>盯盘请求间隔的默认值（BR-014）；数据源上可单独配置</summary>
    public const string WatchIntervalSeconds = "collect:watch_interval_seconds";

    /// <summary>盯盘计划看未来多少分钟（PRD FR-003 主流程第 1 步）</summary>
    public const string WatchPlanMinutes = "collect:watch_plan_minutes";

    /// <summary>延迟后的补采：快间隔（分钟）、按快间隔补采的小时数、之后的慢间隔（分钟）（BR-017）</summary>
    public const string WatchBackfillMinutes = "collect:watch_backfill_minutes";
    public const string WatchBackfillHours = "collect:watch_backfill_hours";
    public const string WatchBackfillSlowMinutes = "collect:watch_backfill_slow_minutes";

    public static readonly IReadOnlyList<ConfigDefinition> Definitions =
    [
        new(RetryDelaySeconds, ConfigValueType.String, RetryDelaySecondsDefault, CollectGroup,
            "采集请求失败后的重试间隔（秒），逗号分隔，个数即最多重试次数；留空表示不重试"),
        new(RunRetentionDays, ConfigValueType.Int, RunRetentionDaysDefault.ToString(CultureInfo.InvariantCulture), CollectGroup,
            "运行记录保留天数，每天清理一次更早的记录"),
        new(SnapshotRetentionDays, ConfigValueType.Int, SnapshotRetentionDaysDefault.ToString(CultureInfo.InvariantCulture), CollectGroup,
            "快照保留天数，每天清理一次；被入库数据引用的快照至少保留 365 天"),
        new(WatchEnabled, ConfigValueType.Bool, "true", CollectGroup,
            "盯盘总开关：关闭后不再新建盯盘，进行中的停止请求"),
        new(WatchIncludeMedium, ConfigValueType.Bool, "false", CollectGroup,
            "盯盘是否包括中重要性的事件；改后下一分钟的盯盘计划生效"),
        Int(WatchLeadSeconds, WatchRule.DefaultLeadSeconds,
            $"盯盘提前秒数的默认值（{WatchRule.MinLeadSeconds} ~ {WatchRule.MaxLeadSeconds}）；指标数据源的\"公布值\"映射上可单独设置"),
        Int(WatchWindowMinutes, WatchRule.DefaultWindowMinutes,
            $"盯盘窗口的默认值：排期后多少分钟结束（{WatchRule.MinWindowMinutes} ~ {WatchRule.MaxWindowMinutes}）；映射上可单独设置"),
        Int(WatchIntervalSeconds, WatchRule.DefaultIntervalSeconds,
            $"盯盘请求间隔的默认值（{WatchRule.MinIntervalSeconds} ~ {WatchRule.MaxIntervalSeconds} 秒），不小于数据源频率上限换算的间隔；数据源上可单独设置"),
        Int(WatchPlanMinutes, WatchRule.DefaultPlanMinutes,
            $"盯盘计划看未来多少分钟（{WatchRule.MinPlanMinutes} ~ {WatchRule.MaxPlanMinutes}），程序保证不短于最大提前秒数加 1 分钟"),
        Int(WatchBackfillMinutes, WatchRule.DefaultBackfillMinutes,
            $"事件延迟后的补采间隔（分钟，{WatchRule.MinBackfillMinutes} ~ {WatchRule.MaxBackfillMinutes}）"),
        Int(WatchBackfillHours, WatchRule.DefaultBackfillHours,
            $"按上面的间隔补采多少小时（1 ~ {WatchRule.MaxBackfillHours}），之后改用慢间隔并告警一次"),
        Int(WatchBackfillSlowMinutes, WatchRule.DefaultBackfillSlowMinutes,
            $"超过上述时长后的补采间隔（分钟，{WatchRule.MinBackfillMinutes} ~ {WatchRule.MaxBackfillMinutes}）"),
    ];

    private static ConfigDefinition Int(string key, int defaultValue, string description)
        => new(key, ConfigValueType.Int, defaultValue.ToString(CultureInfo.InvariantCulture), CollectGroup, description);
}
