using Hangfire;
using Hangfire.Common;
using Suanpan.Infrastructure.Hangfire;
using Xunit;

namespace Suanpan.Infrastructure.Tests.Hangfire;

/// <summary>完成标准：周期任务注册统一按 UTC 解释 cron（PRD NFR-122）</summary>
public class UtcRecurringJobExtensionsTests
{
    [Fact]
    public void RegistersCronInUtc()
    {
        var manager = new CapturingManager();

        manager.AddOrUpdateUtc<SampleJob>("sample", job => job.RunAsync(), "0 * * * *");

        Assert.Equal(TimeZoneInfo.Utc.Id, manager.Options?.TimeZone.Id);
    }

    public sealed class SampleJob
    {
        public Task RunAsync() => Task.CompletedTask;
    }

    private sealed class CapturingManager : IRecurringJobManager
    {
        public RecurringJobOptions? Options { get; private set; }

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options) => Options = options;

        public void Trigger(string recurringJobId)
        {
        }

        public void RemoveIfExists(string recurringJobId)
        {
        }
    }
}
