using System.Collections.Concurrent;
using Suanpan.Core.Ops.Enums;
using Suanpan.Services.Common.Alerts;

namespace Suanpan.Collector.Tests.Fixtures;

/// <summary>记录 Collector 发出的告警，测试按级别和对象查找</summary>
public sealed class RecordingAlertService : IAlertService
{
    public ConcurrentQueue<RaisedAlert> Raised { get; } = new();

    public Task RaiseAsync(AlertLevel level, string subject, string message, CancellationToken ct = default)
    {
        Raised.Enqueue(new RaisedAlert(level, subject));
        return Task.CompletedTask;
    }
}

public sealed record RaisedAlert(AlertLevel Level, string Subject);
