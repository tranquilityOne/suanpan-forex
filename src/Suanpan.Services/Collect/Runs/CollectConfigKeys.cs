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

    public static readonly IReadOnlyList<ConfigDefinition> Definitions =
    [
        new(RetryDelaySeconds, ConfigValueType.String, RetryDelaySecondsDefault, CollectGroup,
            "采集请求失败后的重试间隔（秒），逗号分隔，个数即最多重试次数；留空表示不重试"),
        new(RunRetentionDays, ConfigValueType.Int, RunRetentionDaysDefault.ToString(CultureInfo.InvariantCulture), CollectGroup,
            "运行记录保留天数，每天清理一次更早的记录"),
    ];
}
