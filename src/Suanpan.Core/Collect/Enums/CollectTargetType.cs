namespace Suanpan.Core.Collect.Enums;

/// <summary>采集任务的目标数据类型（BR-007），由解析器决定；中文显示名见 CollectDisplayNames</summary>
public enum CollectTargetType : short
{
    /// <summary>日历排期</summary>
    Calendar = 1,

    /// <summary>公布值</summary>
    Release = 2,

    /// <summary>历史数据</summary>
    History = 3,

    /// <summary>央行利率</summary>
    CentralBankRate = 4,

    /// <summary>参考汇率</summary>
    ReferenceRate = 5,

    /// <summary>新闻</summary>
    News = 6,
}
