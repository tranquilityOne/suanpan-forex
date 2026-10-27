namespace Suanpan.Collector.Tests.Fixtures;

/// <summary>
/// 等待 Collector 在后台处理完：轮询数据库，到达预期状态即返回，超过上限报错（采集设计 §12）。
/// 不用固定时长的等待（后端约定 §13.4），状态到了就继续
/// </summary>
public static class Eventually
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public static async Task<T> GetAsync<T>(Func<Task<T>> read, Func<T, bool> done, Func<T, string> describe, TimeSpan? within = null)
    {
        var limit = within ?? TimeSpan.FromSeconds(30);
        using var timeout = new CancellationTokenSource(limit);
        using var timer = new PeriodicTimer(PollInterval);
        while (true)
        {
            var value = await read();
            if (done(value))
            {
                return value;
            }

            try
            {
                await timer.WaitForNextTickAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"{limit.TotalSeconds:0} 秒内没有等到预期的状态，最后一次读到：{describe(value)}");
            }
        }
    }
}
