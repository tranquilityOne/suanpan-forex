using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Suanpan.Data;
using Suanpan.Infrastructure;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Security;
using Xunit;

namespace Suanpan.Services.Tests;

public class ServicesServiceCollectionExtensionsTests
{
    /// <summary>
    /// 依赖注入的冒烟测试：ValidateOnBuild 检查全部注册的依赖都能满足；
    /// 再取几个依赖链最长、或构造时读取配置的服务，确认能真正创建出来。不连接数据库。
    /// </summary>
    [Fact]
    public void AllRegistrationsCanBeBuiltAndResolved()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Main"] = "Host=127.0.0.1;Database=resolve_only",
            ["Security:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Redis:Configuration"] = "127.0.0.1:1",
        }).Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddSuanpanData(configuration, "test")
            .AddSuanpanRedis(configuration)
            .AddSuanpanServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserManageService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISecretProtector>());
        Assert.NotEmpty(scope.ServiceProvider.GetServices<IAuthorizationHandler>());
    }
}
