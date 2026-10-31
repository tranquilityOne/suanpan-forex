using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Shared.Messages.Calendar;

/// <summary>
/// 公布过的日历事件（已公布，或官方更正进入待复核）被人工改了公布值、前值、前值修正（BR-052，日历设计 §6.3）。
/// 后台在事务提交后发布，快讯模块（FR-016）据此提示编辑处理依据这些数值的快讯；FR-016 实现前没有接收方
/// </summary>
/// <param name="EventId">日历事件 id（cal_event）</param>
/// <param name="Fields">改了的字段</param>
/// <param name="ChangedBy">操作人的登录名</param>
public sealed record EventValueEditedEvent(
    long EventId,
    string IndicatorCode,
    string ReferencePeriod,
    IReadOnlyList<EventField> Fields,
    string ChangedBy) : IntegrationMessage;
