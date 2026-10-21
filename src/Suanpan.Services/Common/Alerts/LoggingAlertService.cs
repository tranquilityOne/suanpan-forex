using Microsoft.Extensions.Logging;
using Suanpan.Core.Ops.Enums;

namespace Suanpan.Services.Common.Alerts;

/// <summary>M1 阶段的告警实现：只写 Error 级日志，由日志系统收集。Ops 模块（FR-025）完成后替换为真正发送告警的实现。</summary>
public sealed class LoggingAlertService(ILogger<LoggingAlertService> logger) : IAlertService
{
    public Task RaiseAsync(AlertLevel level, string subject, string message, CancellationToken ct = default)
    {
        logger.LogError("告警（{AlertLevel}）{AlertSubject}：{AlertMessage}", level, subject, message);
        return Task.CompletedTask;
    }
}
