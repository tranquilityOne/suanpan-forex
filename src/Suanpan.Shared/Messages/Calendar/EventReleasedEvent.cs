namespace Suanpan.Shared.Messages.Calendar;

/// <summary>
/// 日历事件变为"已公布"（EV-004，BR-045，日历设计 §6.3）：公布值入库或编辑确认后在事务提交后发布，Job 据此生成快讯（FR-014）。
/// 公布值不放进消息，接收方按 EventId 读库，拿到的总是最新值
/// </summary>
/// <param name="EventId">日历事件 id（cal_event）</param>
/// <param name="ReleaseBatchId">发布批次，同一份报告的事件相同（BR-076）；补建的事件为空</param>
/// <param name="DetectedAt">检测时间（UTC，BR-045）</param>
public sealed record EventReleasedEvent(
    long EventId,
    string IndicatorCode,
    string ReferencePeriod,
    string? ReleaseBatchId,
    DateTime DetectedAt) : IntegrationMessage;
