namespace Suanpan.Infrastructure.Tests.Fixtures;

/// <summary>可设置的固定时间（后端约定 §13.4：测试中不直接取当前时间）</summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
