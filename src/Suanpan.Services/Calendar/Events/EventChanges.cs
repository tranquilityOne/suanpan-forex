using System.Globalization;
using System.Text.Json;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Services.Calendar.Events;

/// <summary>谁在什么时候改的：采集时带数据源和运行记录（cal_event_change，日历设计 §2.1）</summary>
public sealed record EventChangeActor(ChangeActorKind Kind, string ChangedBy, DateTime ChangedAt, long? SourceId = null, long? RunId = null);

/// <summary>
/// 日历事件变更历史的写法（cal_event_change.old_value / new_value 为 jsonb）：改期为 {"at": UTC, "local": 当地时间, "precision": 精度}，
/// 状态为状态值，数值为数值字符串。排期入库、公布值入库、后台修改共用
/// </summary>
public static class EventChanges
{
    public static CalendarEventChange Create(
        long eventId, EventChangeKind kind, string? oldValue, string? newValue, EventChangeActor actor, string? reason = null) => new()
    {
        EventId = eventId,
        Kind = kind,
        OldValue = oldValue,
        NewValue = newValue,
        ActorKind = actor.Kind,
        SourceId = actor.SourceId,
        RunId = actor.RunId,
        ChangedBy = actor.ChangedBy,
        ChangedAt = actor.ChangedAt,
        Reason = reason,
    };

    public static string Schedule(DateTime? scheduledAt, DateTime? scheduledLocal, TimePrecision precision)
        => JsonSerializer.Serialize(new
        {
            at = scheduledAt,
            local = scheduledLocal?.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            precision = (short)precision,
        });

    public static string Status(CalendarEventStatus status) => JsonSerializer.Serialize((short)status);

    /// <summary>数值写成字符串，不经过浮点数，保留原有的小数位</summary>
    public static string? Number(decimal? value) => value is { } number ? JsonSerializer.Serialize(number.ToString(CultureInfo.InvariantCulture)) : null;
}
