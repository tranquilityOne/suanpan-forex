namespace Suanpan.Core.Calendar.Enums;

/// <summary>变更是采集还是人工做的（cal_event_change.actor_kind）</summary>
public enum ChangeActorKind : short
{
    /// <summary>采集</summary>
    Collector = 1,

    /// <summary>人工</summary>
    User = 2,
}
