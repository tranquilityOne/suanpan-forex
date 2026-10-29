using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Reference.Entities;

namespace Suanpan.Services.Calendar.Releases;

/// <summary>
/// 公布值写入事件前的质量校验（FR-026，日历设计 §5.6）：格式、合理范围、标准差、主备一致性，并记录每次校验（BR-136）。
/// 实现由 FR-026（Ops 模块）提供；在那之前注册 <see cref="PassThroughReleaseValidator"/>，全部通过。
/// 已公布值再次采集到不同数值（BR-133 的"变更"）由入库服务直接判断，不经过这里
/// </summary>
public interface IReleaseValidator
{
    Task<ReleaseValidation> ValidateAsync(ReleaseCandidate candidate, CancellationToken ct);
}

/// <summary>要写入的公布值：指标、事件、按小数位舍入后的值、来自哪个数据源及其优先级、其他数据源的值（主备一致性）</summary>
public sealed record ReleaseCandidate(
    Indicator Indicator,
    CalendarEvent Event,
    decimal Value,
    decimal? ValueUpper,
    long SourceId,
    short? Priority,
    IReadOnlyList<CalendarEventValue> OtherSourceValues);

/// <summary>校验结果：没通过时列出命中的规则（写进待复核的原因）</summary>
public sealed record ReleaseValidation(bool Passed, IReadOnlyList<string> FailedRules)
{
    public static readonly ReleaseValidation Pass = new(true, []);

    public static ReleaseValidation Fail(params string[] rules) => new(false, rules);
}

/// <summary>FR-026 完成前的校验：全部通过（日历设计 §5.6）。FR-026 用 Replace 换掉这个注册</summary>
public sealed class PassThroughReleaseValidator : IReleaseValidator
{
    public Task<ReleaseValidation> ValidateAsync(ReleaseCandidate candidate, CancellationToken ct) => Task.FromResult(ReleaseValidation.Pass);
}
