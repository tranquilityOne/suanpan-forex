using Suanpan.Shared.Messages.Calendar;

namespace Suanpan.Services.Calendar.Schedules.Models;

/// <summary>写入一条排期的结果：新建、更新还是跳过；改期时带上提交后要发布的消息（EV-003）</summary>
public sealed record ScheduleApplyResult(ScheduleApplyOutcome Outcome, EventRescheduledEvent? Rescheduled = null)
{
    public static readonly ScheduleApplyResult Inserted = new(ScheduleApplyOutcome.Inserted);

    public static readonly ScheduleApplyResult Skipped = new(ScheduleApplyOutcome.Skipped);
}

public enum ScheduleApplyOutcome
{
    /// <summary>新建了事件</summary>
    Inserted,

    /// <summary>改期、改状态或由本数据源接管</summary>
    Updated,

    /// <summary>不变、事件已结束，或由别的数据源维护</summary>
    Skipped,
}
