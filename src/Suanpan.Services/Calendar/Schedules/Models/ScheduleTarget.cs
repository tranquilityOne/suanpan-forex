using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Calendar.Schedules.Models;

/// <summary>排期匹配到的指标：编码、发布频率（校验参考期、找上一期）、所属经济体的时区（换算排期）</summary>
public sealed record ScheduleIndicator(string Code, ReleaseFrequency Frequency, string TimeZone);

/// <summary>
/// 要写入的一条排期（日历设计 §4.3）：指标的一期、换算好的排期，以及维护它的数据源和发布标识
/// </summary>
/// <param name="ReleaseBatchId">发布批次（BR-076、BR-156）；没有日期时为空</param>
/// <param name="RunId">产生它的运行记录，写进变更历史；重新匹配未匹配条目时为空</param>
public sealed record ScheduleTarget(
    ScheduleIndicator Indicator,
    string ReferencePeriod,
    ResolvedSchedule Schedule,
    TimePrecision Precision,
    long SourceId,
    string ReleaseKey,
    string? ReleaseBatchId,
    long? RunId);
