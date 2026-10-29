using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Suanpan.Infrastructure.Http;

/// <summary>
/// 启动时检查采集请求的联系邮箱（BR-005，采集设计 §5.4）：没配置时告警，不阻止启动，请求的 User-Agent 只有产品标识
/// </summary>
internal sealed class CollectHttpStartupCheck(IOptions<CollectHttpOptions> options, ILogger<CollectHttpStartupCheck> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ContactEmail))
        {
            logger.LogWarning(
                "没有配置 Collect:ContactEmail，采集请求的 User-Agent 不带联系邮箱（BR-005）。本地用 user-secrets 设置，其他环境用环境变量 SUANPAN__Collect__ContactEmail（见 README）");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
