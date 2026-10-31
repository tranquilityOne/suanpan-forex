namespace Suanpan.Collector.Tests.Fixtures;

/// <summary>
/// 手动推进的时钟（后端约定 §13.4：测试中不取当前时间、不真的等待）：经 TimeProvider 创建的定时器（Task.Delay、PeriodicTimer）
/// 只在 <see cref="Advance"/> 推进到期时触发。盯盘的测试用它控制"排期前 60 秒""T+10 分钟"这些时刻
/// </summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => GetUtcNow().UtcTicks;

    /// <summary>向前推进，期间到期的定时器按到期时间依次触发（回调在当前线程执行）</summary>
    public void Advance(TimeSpan delta)
    {
        DateTimeOffset target;
        lock (_gate)
        {
            target = _now + delta;
        }

        while (true)
        {
            ManualTimer? due;
            lock (_gate)
            {
                due = _timers.Where(t => t.DueAt <= target).MinBy(t => t.DueAt);
                if (due is null)
                {
                    _now = target;
                    return;
                }

                _now = due.DueAt!.Value > _now ? due.DueAt.Value : _now;
                due.Advance();
            }

            due.Invoke();
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                clock._timers.Remove(this);
                _period = period;
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock._now + (dueTime > TimeSpan.Zero ? dueTime : TimeSpan.Zero);
                if (DueAt is not null)
                {
                    clock._timers.Add(this);
                }
            }

            return true;
        }

        /// <summary>到期后：周期定时器排到下一次，一次性的移除；在锁内调用</summary>
        public void Advance()
        {
            if (_period > TimeSpan.Zero && _period != Timeout.InfiniteTimeSpan)
            {
                DueAt += _period;
            }
            else
            {
                DueAt = null;
                clock._timers.Remove(this);
            }
        }

        public void Invoke() => callback(state);

        public void Dispose()
        {
            lock (clock._gate)
            {
                clock._timers.Remove(this);
                DueAt = null;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
