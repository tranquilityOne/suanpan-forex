using Suanpan.Services.Calendar.Forecasts;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Sys.Configs.Models;

namespace Suanpan.Services.Sys.Configs;

/// <summary>
/// 全部模块的配置项定义，系统配置页面据此显示和校验（后端约定 §7）。
/// 各模块在自己的配置键类（如 SysConfigKeys、CollectConfigKeys）中登记 Definitions，再在这里汇总
/// </summary>
public static class ConfigDefinitions
{
    public static readonly IReadOnlyList<ConfigDefinition> All = [.. SysConfigKeys.Definitions, .. CollectConfigKeys.Definitions, .. CalendarConfigKeys.Definitions];
}
