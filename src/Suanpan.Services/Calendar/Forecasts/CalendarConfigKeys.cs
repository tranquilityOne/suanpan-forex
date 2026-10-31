using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Configs.Models;

namespace Suanpan.Services.Calendar.Forecasts;

/// <summary>
/// 财经日历的配置键和默认值（日历设计 §11），读取方式同 SysConfigKeys。
/// 新增配置项时同时加入 <see cref="Definitions"/>（由 ConfigDefinitions 汇总），系统配置页面才能显示和修改
/// </summary>
public static class CalendarConfigKeys
{
    private const string CalendarGroup = "财经日历";

    /// <summary>预期值来源说明的常用选项，逗号分隔（BR-054，§12 第 19 项）；默认空</summary>
    public const string ForecastNoteOptions = "calendar:forecast_note_options";

    public static readonly IReadOnlyList<ConfigDefinition> Definitions =
    [
        new(ForecastNoteOptions, ConfigValueType.String, string.Empty, CalendarGroup,
            "预期值来源说明的常用选项，逗号分隔；录入时作为提示，也可以自由填写"),
    ];
}
