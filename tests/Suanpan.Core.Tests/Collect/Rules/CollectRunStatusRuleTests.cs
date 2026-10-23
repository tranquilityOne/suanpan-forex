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
}
