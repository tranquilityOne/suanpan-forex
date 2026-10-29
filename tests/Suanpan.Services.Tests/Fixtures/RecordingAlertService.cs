using Suanpan.Core.Ops.Enums;
using Suanpan.Services.Common.Alerts;

namespace Suanpan.Services.Tests.Fixtures;

/// <summary>告警的替身：记下级别和对象，测试按它们查找（不比对告警文字）</summary>
internal sealed class RecordingAlertService : IAlertService
{
    public List<(AlertLevel Level, string Subject)> Raised { get; } = [];

    public Task RaiseAsync(AlertLevel level, string subject, string message, CancellationToken ct = default)
    {
        Raised.Add((level, subject));
        return Task.CompletedTask;
    }
}
