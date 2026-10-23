using Suanpan.Core.Collect.Enums;

namespace Suanpan.Core.Collect.Rules;

/// <summary>
/// 数据采集模块枚举的中文显示名（采集设计 §7.1），只在后台使用。
/// 枚举增加取值时在这里登记，每个值都要有对应项（测试会检查）。
/// </summary>
public static class CollectDisplayNames
{
    private static readonly Dictionary<Enum, string> Map = new()
    {
        [DataSourceType.OfficialApi] = "官方 API",
        [DataSourceType.OfficialWeb] = "官方网页",
        [DataSourceType.Feed] = "RSS / Atom",
        [DataSourceType.FileDownload] = "文件下载",

        [RateLimitUnit.PerSecond] = "次/秒",
        [RateLimitUnit.PerMinute] = "次/分",

        [ComplianceClass.GovernmentPublic] = "政府公开数据",
        [ComplianceClass.InstitutionLicense] = "机构许可",
        [ComplianceClass.Pending] = "待确认",

        [RobotsResult.Allowed] = "允许",
        [RobotsResult.NotApplicable] = "不涉及（API）",
        [RobotsResult.Disallowed] = "不允许",

        [CollectTargetType.Calendar] = "日历排期",
        [CollectTargetType.Release] = "公布值",
        [CollectTargetType.History] = "历史数据",
        [CollectTargetType.CentralBankRate] = "央行利率",
        [CollectTargetType.ReferenceRate] = "参考汇率",
        [CollectTargetType.News] = "新闻",

        [CollectRunTrigger.Scheduled] = "定时",
        [CollectRunTrigger.Watch] = "盯盘",
        [CollectRunTrigger.Manual] = "手动",
        [CollectRunTrigger.Reparse] = "重新解析",

        [CollectRunStatus.Queued] = "排队",
        [CollectRunStatus.Running] = "运行中",
        [CollectRunStatus.Succeeded] = "成功",
        [CollectRunStatus.PartiallySucceeded] = "部分成功",
        [CollectRunStatus.Failed] = "失败",
        [CollectRunStatus.Skipped] = "跳过",
    };

    /// <summary>取枚举值的中文显示名；没有登记的值显示枚举名</summary>
    public static string Get(Enum value) => Map.TryGetValue(value, out var name) ? name : value.ToString();

    /// <summary>是否已登记（测试用来检查每个枚举值都有显示名）</summary>
    public static bool Contains(Enum value) => Map.ContainsKey(value);
}
