namespace Suanpan.Shared.Messages.Calendar;

/// <summary>
/// 日历事件进入"待复核"（EV-005，BR-046、BR-134，日历设计 §6.3）：校验未通过或官方更正，入库在事务提交后发布；
/// 告警由入库时直接调用告警服务，Job 据此做告警以外的后续处理（FR-025）
/// </summary>
/// <param name="EventId">日历事件 id（cal_event）</param>
/// <param name="Reason">命中的校验规则，或"官方更正"</param>
public sealed record EventHeldForReviewEvent(long EventId, string IndicatorCode, string Reason) : IntegrationMessage;
