using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Suanpan.Services.Sys.Auth;

/// <summary>Crm 启动时创建初始管理员；失败只记 Error 日志，不阻止启动</summary>
public sealed class AdminSeedHostedService(IServiceScopeFactory scopeFactory, ILogger<AdminSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "创建初始管理员失败");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
