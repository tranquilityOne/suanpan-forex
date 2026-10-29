using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Shared.Messages.Calendar;

/// <summary>
/// 日历事件改期了（EV-003，BR-039，日历设计 §6.3）：官方日程中的排期变了，排期入库在事务提交后发布。
/// 接收方：Job（预告快讯，FR-015，M3）、Portal（缓存刷新，FR-023 接入后）；在那之前没有队列接收，消息被丢弃
/// </summary>
/// <param name="EventId">日历事件 id（cal_event）</param>
/// <param name="OldScheduledAt">原来的排期（UTC）；没有日期时为空</param>
/// <param name="NewScheduledAt">新的排期（UTC）</param>
/// <param name="NewPrecision">新的时间精度</param>
public sealed record EventRescheduledEvent(
    long EventId,
    string IndicatorCode,
    string ReferencePeriod,
    DateTime? OldScheduledAt,
    DateTime? NewScheduledAt,
    TimePrecision NewPrecision) : IntegrationMessage;
