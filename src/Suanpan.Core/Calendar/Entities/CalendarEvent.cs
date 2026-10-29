using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Common.Auditing;

namespace Suanpan.Core.Calendar.Entities;

/// <summary>
/// 日历事件（cal_event，日历设计 §2.1）：一个指标的一期发布。指标 + 参考期唯一（BR-036）。
/// 排期只来自数据源（DEC-024）；公布值、前值、预期值等可以人工修改并锁定（BR-060）。
/// 每次修改都要把 <see cref="RowVersion"/> 加一（乐观锁，ERR-EVT-009）
/// </summary>
public class CalendarEvent : AuditedEntity
{
    public long Id { get; set; }

    public string IndicatorCode { get; set; } = string.Empty;

    /// <summary>参考期，写法见 ReferencePeriodRule（如 2026-09、2026-Q3）</summary>
    public string ReferencePeriod { get; set; } = string.Empty;

    public CalendarEventStatus Status { get; set; } = CalendarEventStatus.Scheduled;

    public EventOrigin Origin { get; set; }

    /// <summary>发布批次（BR-076、BR-156）：{数据源 id}:{发布标识}:{当地日期}，同一份报告的事件相同；补建的为空</summary>
    public string? ReleaseBatchId { get; set; }

    /// <summary>维护排期的数据源（日历设计 §4.3）；补建的、编辑"保留"过的为空，直到某个数据源的日程接管</summary>
    public long? ScheduleSourceId { get; set; }

    /// <summary>排期来自哪个发布标识，判断"从日程中消失"时用（BR-040）</summary>
    public string? ScheduleReleaseKey { get; set; }

    /// <summary>机构当地时间，只用于保留原始值（Kind 为 Unspecified）；仅日期时时间部分为 00:00；待定且没有日期时为空</summary>
    public DateTime? ScheduledLocal { get; set; }

    /// <summary>IANA 时区，写入排期时取指标所属经济体的时区</summary>
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>
    /// UTC，用于排序、筛选和盯盘：精确到分钟时为换算结果；仅日期、待定时为当地日期 00:00 换算的结果，只用于排序；没有日期时为空
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    public TimePrecision TimePrecision { get; set; }

    /// <summary>换算时当地时刻属于哪种情况（BR-037），只标记，不影响状态</summary>
    public LocalTimeResolution LocalTimeResolution { get; set; } = LocalTimeResolution.Exact;

    /// <summary>公布值；只有已公布（或待复核后经编辑确认）才有值，待复核时候选值在 cal_event_value 中</summary>
    public decimal? Actual { get; set; }

    /// <summary>区间型指标的公布值上限</summary>
    public decimal? ActualUpper { get; set; }

    /// <summary>公布值的原始字符串（BR-043）</summary>
    public string? ActualRaw { get; set; }

    /// <summary>公布值取自哪个数据源；人工录入时为空</summary>
    public long? ActualSourceId { get; set; }

    /// <summary>检测时间（BR-045），入库时延 = 检测时间 − 排期时间（BR-019）</summary>
    public DateTime? DetectedAt { get; set; }

    /// <summary>来源快照（BR-045）</summary>
    public long? SourceSnapshotId { get; set; }

    /// <summary>预期值（FR-010）</summary>
    public decimal? Forecast { get; set; }

    public decimal? ForecastUpper { get; set; }

    public ForecastType? ForecastType { get; set; }

    /// <summary>预期值的来源说明，仅内部可见（BR-054）</summary>
    public string? ForecastNote { get; set; }

    /// <summary>前值（BR-044）</summary>
    public decimal? Previous { get; set; }

    public decimal? PreviousUpper { get; set; }

    /// <summary>前值修正：本次发布中上一期的修正值（BR-044）</summary>
    public decimal? PreviousRevised { get; set; }

    /// <summary>本期的修正后数值：以后各次发布对本期的修正（BR-044），不可锁定</summary>
    public decimal? RevisedValue { get; set; }

    /// <summary>已锁定的字段（BR-060、BR-061），读写见 EventLockRule</summary>
    public List<EventFieldLock> LockedFields { get; set; } = [];

    /// <summary>乐观锁（ERR-EVT-009）：每次修改加一，人工保存时带上读取时的值</summary>
    public int RowVersion { get; set; }
}
