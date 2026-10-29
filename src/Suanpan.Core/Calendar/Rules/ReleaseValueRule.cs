using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 公布值的取舍（日历设计 §3.5、§5.2 ~ §5.4，BR-043 ~ BR-046、BR-157）：纯函数，入库服务按返回的计划写库。
/// 输入一个指标在数据中的各期（已按小数位舍入）和相关事件，输出最新一期怎么处理、前值和前值修正、更早各期的修正后数值
/// </summary>
public static class ReleaseValueRule
{
    /// <summary>按指标的小数位四舍五入，.5 远离零（BR-043），不用 .NET 默认的银行家舍入</summary>
    public static decimal Round(decimal value, short decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    /// <param name="series">数据中的各期，参考期不重复，顺序不限</param>
    /// <param name="events">数据中各期对应的事件（参考期 → 事件），没有事件的期不在其中</param>
    /// <param name="hasReleasedEvent">该指标是否有公布过的事件：状态为"已公布"或公布值不为空（BR-157）</param>
    public static ReleasePlan Plan(IReadOnlyList<ReleasePoint> series, IReadOnlyDictionary<string, CalendarEvent> events, bool hasReleasedEvent)
    {
        if (series.Count == 0)
        {
            throw new ArgumentException("没有数据", nameof(series));
        }

        var ordered = series.OrderBy(p => p.Period, StringComparer.Ordinal).ToList();
        var latest = ordered[^1];
        var previous = ordered.Count > 1 ? ordered[^2] : null;
        events.TryGetValue(latest.Period, out var latestEvent);
        var previousEvent = previous is not null && events.TryGetValue(previous.Period, out var found) ? found : null;

        var action = LatestActionFor(latest, latestEvent, hasReleasedEvent);
        var lockConflict = action == LatestAction.ValueOnly && latestEvent is not null
            && EventLockRule.IsLocked(latestEvent.LockedFields, EventField.Actual)
            && !Same(latest, latestEvent.Actual, latestEvent.ActualUpper);

        // 前值和前值修正（§5.3）：数据中有上一期时才确定；上一期有公布过的事件时前值取它的公布值，数据中的值不同就是前值修正
        PreviousValues? previousValues = null;
        if (previous is not null && action != LatestAction.InitialPrevious)
        {
            previousValues = previousEvent?.Actual is { } released
                ? new PreviousValues(released, previousEvent.ActualUpper, Same(previous, released, previousEvent.ActualUpper) ? null : previous.Value)
                : new PreviousValues(previous.Value, previous.Upper, null);
        }

        // 更早各期（含上一期）的修正后数值：已有更新的一期，变化都按修正处理，不进待复核（BR-044、BR-046）
        var revisions = ordered.Take(ordered.Count - 1)
            .Where(p => events.TryGetValue(p.Period, out var e) && e.Actual is { } actual && p.Value != (e.RevisedValue ?? actual))
            .Select(p => new Revision(events[p.Period], p.Value))
            .ToList();

        return new ReleasePlan(latest, action, lockConflict, previousValues, revisions);
    }

    private static LatestAction LatestActionFor(ReleasePoint latest, CalendarEvent? latestEvent, bool hasReleasedEvent)
    {
        if (latestEvent is null)
        {
            return hasReleasedEvent ? LatestAction.Supplement : LatestAction.InitialPrevious;
        }

        if (latestEvent.Status == CalendarEventStatus.Cancelled || EventLockRule.IsLocked(latestEvent.LockedFields, EventField.Actual))
        {
            return LatestAction.ValueOnly;
        }

        if (CalendarEventStatusRule.IsUnreleased(latestEvent.Status))
        {
            return LatestAction.Release;
        }

        return latestEvent.Status == CalendarEventStatus.Released && !Same(latest, latestEvent.Actual, latestEvent.ActualUpper)
            ? LatestAction.Correction
            : LatestAction.ValueOnly;
    }

    private static bool Same(ReleasePoint point, decimal? value, decimal? upper) => point.Value == value && point.Upper == upper;
}

/// <summary>数据中的一期，值已按指标小数位舍入</summary>
public sealed record ReleasePoint(string Period, decimal Value, decimal? Upper);

/// <summary>最新一期怎么处理（日历设计 §5.2 的表）</summary>
public enum LatestAction
{
    /// <summary>没有事件、该指标有公布过的事件：补建后按"未公布"处理（ERR-EVT-006）</summary>
    Supplement,

    /// <summary>没有事件、该指标还没有公布过的事件（刚开始采集，BR-157）：不补建，数值填为下一个事件的前值</summary>
    InitialPrevious,

    /// <summary>事件未公布：校验后变为"已公布"或"待复核"</summary>
    Release,

    /// <summary>事件已公布、值不同，仍是最新一期：官方更正，进入待复核（BR-046）</summary>
    Correction,

    /// <summary>只记各数据源的数值：事件已取消、公布值被锁定、已公布且值相同、待复核</summary>
    ValueOnly,
}

/// <param name="Latest">数据中的最新一期</param>
/// <param name="Action">最新一期怎么处理</param>
/// <param name="ActualLockConflict">公布值被锁定且与采集值不同，要写锁定冲突（BR-062）</param>
/// <param name="Previous">最新一期事件的前值和前值修正；数据中没有上一期或刚开始采集时为空（不改）</param>
/// <param name="Revisions">更早各期要更新的修正后数值</param>
public sealed record ReleasePlan(
    ReleasePoint Latest,
    LatestAction Action,
    bool ActualLockConflict,
    PreviousValues? Previous,
    IReadOnlyList<Revision> Revisions);

/// <summary>前值（连同上限）和前值修正（没有修正时为空）</summary>
public sealed record PreviousValues(decimal Previous, decimal? PreviousUpper, decimal? PreviousRevised);

/// <summary>某一期事件的修正后数值要改成的值</summary>
public sealed record Revision(CalendarEvent Event, decimal RevisedValue);
