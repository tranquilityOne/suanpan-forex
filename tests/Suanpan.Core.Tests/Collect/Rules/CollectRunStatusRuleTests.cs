using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Collect.Rules;

/// <summary>采集设计 §3.3 的状态图</summary>
public class CollectRunStatusRuleTests
{
    [Theory]
    [InlineData(CollectRunStatus.Queued, CollectRunStatus.Running)]
    [InlineData(CollectRunStatus.Queued, CollectRunStatus.Skipped)]          // 同一任务已在运行（BR-010）
    [InlineData(CollectRunStatus.Queued, CollectRunStatus.Failed)]           // 长时间未开始（ERR-COL-008）
    [InlineData(CollectRunStatus.Running, CollectRunStatus.Running)]         // 等待重试
    [InlineData(CollectRunStatus.Running, CollectRunStatus.Succeeded)]
    [InlineData(CollectRunStatus.Running, CollectRunStatus.PartiallySucceeded)]
    [InlineData(CollectRunStatus.Running, CollectRunStatus.Failed)]
    [InlineData(CollectRunStatus.Running, CollectRunStatus.Skipped)]         // 重试前发现已停用
    public void AllowsTransitionsInTheDiagram(CollectRunStatus from, CollectRunStatus to)
        => Assert.True(CollectRunStatusRule.CanTransition(from, to));

    [Theory]
    [InlineData(CollectRunStatus.Queued, CollectRunStatus.Succeeded)]        // 没开始就成功
    [InlineData(CollectRunStatus.Running, CollectRunStatus.Queued)]
    [InlineData(CollectRunStatus.Succeeded, CollectRunStatus.Running)]       // 结束后不再变化
    [InlineData(CollectRunStatus.Failed, CollectRunStatus.Failed)]
    [InlineData(CollectRunStatus.Skipped, CollectRunStatus.Running)]
    public void RejectsOtherTransitions(CollectRunStatus from, CollectRunStatus to)
        => Assert.False(CollectRunStatusRule.CanTransition(from, to));

    private static readonly DateTime Now = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>ERR-COL-009：等待重试的，重试时间过去 5 分钟以上（重试消息丢失）才算中断；刚好 5 分钟不算</summary>
    [Theory]
    [InlineData(-301, true)]
    [InlineData(-300, false)]
    [InlineData(60, false)]
    public void IsInterrupted_WaitingRetry(int nextAttemptOffsetSeconds, bool expected)
    {
        var run = Running(startedAt: Now.AddHours(-1));
        run.NextAttemptAt = Now.AddSeconds(nextAttemptOffsetSeconds);

        Assert.Equal(expected, CollectRunStatusRule.IsInterrupted(run, Timeout, Now));
    }

    /// <summary>ERR-COL-009：正在请求的，以最近一条请求记录的开始时间计，超过"超时 + 5 分钟"算中断；还没有请求记录时用开始时间</summary>
    [Theory]
    [InlineData(-331, null, true)]
    [InlineData(-3600, -330, false)]
    [InlineData(-3600, -331, true)]
    public void IsInterrupted_Requesting(int startedOffsetSeconds, int? lastAttemptOffsetSeconds, bool expected)
    {
        var run = Running(startedAt: Now.AddSeconds(startedOffsetSeconds));
        if (lastAttemptOffsetSeconds is { } offset)
        {
            run.Attempts.Add(new CollectAttempt { No = 1, StartedAt = Now.AddSeconds(startedOffsetSeconds) });
            run.Attempts.Add(new CollectAttempt { No = 2, StartedAt = Now.AddSeconds(offset) });
        }

        Assert.Equal(expected, CollectRunStatusRule.IsInterrupted(run, Timeout, Now));
    }

    [Fact]
    public void IsInterrupted_OnlyRunningRuns()
    {
        var run = Running(startedAt: Now.AddDays(-1));
        run.Status = CollectRunStatus.Queued;

        Assert.False(CollectRunStatusRule.IsInterrupted(run, Timeout, Now));
    }

    private static CollectRun Running(DateTime startedAt) => new()
    {
        Status = CollectRunStatus.Running,
        QueuedAt = startedAt,
        StartedAt = startedAt,
    };
}
