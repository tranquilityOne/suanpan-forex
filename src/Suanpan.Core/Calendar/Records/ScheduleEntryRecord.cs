using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Records;

/// <summary>
/// 排期解析器（目标数据类型"日历排期"）输出的一条日程条目（日历设计 §4.1），交给排期入库。
/// 放在 Core 中：Collector 的解析器和 Services 的入库服务都引用
/// </summary>
/// <param name="ReleaseKey">发布标识，对应排期映射的外部标识（BR-034、BR-156），如 BLS 的 empsit</param>
/// <param name="ReleaseName">发布名称，未匹配条目页面显示用，如 Employment Situation</param>
/// <param name="ReferencePeriod">参考期，按 ReferencePeriodRule 的写法；官方日程不写参考期时由解析器推算，推算不出时为空（该条入库失败）</param>
/// <param name="LocalDate">机构当地日期；待定时可以为空</param>
/// <param name="LocalTime">机构当地时间；精确到分钟时必填，其他精度为空。时区不由解析器给出，入库时取指标所属经济体的时区</param>
/// <param name="Precision">时间精度（BR-038）</param>
/// <param name="ResponseIndex">来自第几个响应，对应入库请求中的快照 id（采集设计 §5.3）</param>
public sealed record ScheduleEntryRecord(
    string ReleaseKey,
    string ReleaseName,
    string? ReferencePeriod,
    DateOnly? LocalDate,
    TimeOnly? LocalTime,
    TimePrecision Precision,
    int ResponseIndex);
