using System.Net;
using System.Security.Claims;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Unmatched;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Security;
using Suanpan.Shared.Auth;

namespace Suanpan.Crm.Tests.Fixtures;

/// <summary>
/// 在内存中启动 Crm（WebApplicationFactory），把依赖数据库的服务和 Hangfire 存储换成假实现：
/// 不连数据库、Redis、RabbitMQ，不需要 Docker。每个测试新建一个，假服务的状态互不影响。
/// 请求默认来自本机回环地址（即本机的反向代理），用请求头 X-Test-Remote-Ip 模拟其他来源。
/// 默认用 Testing 环境；只在开发环境可用的页面传 Development。
/// </summary>
public sealed class CrmApp(string environment = "Testing") : WebApplicationFactory<Program>
{
    public const string BootstrapIp = "198.51.100.9";

    public const string RemoteIpHeader = "X-Test-Remote-Ip";

    public FakeConfigService Config { get; } = new();

    public FakeIpWhitelistService Whitelist { get; } = new();

    public FakePermissionService Permissions { get; } = new();

    public RecordingOpLogService OpLogs { get; } = new();

    public FakeMenuService Menus { get; } = new();

    public FakeIndicatorService Indicators { get; } = new();

    public FakeDataSourceService DataSources { get; } = new();

    public FakeCollectRunService CollectRuns { get; } = new();

    public FakeCalendarEventService CalendarEvents { get; } = new();

    public FakeUnmatchedEntryService Unmatched { get; } = new();

    public FakeSecurityStampValidator Stamps { get; } = new();

    public FakeAuthService Auth => field ??= new FakeAuthService(Stamps);

    /// <summary>
    /// 模拟浏览器：不自动跟随跳转（测试要断言 302 的目标）；自动保存 Cookie。用 https 地址，Testing 环境的 Cookie 带 Secure
    /// </summary>
    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    /// <summary>用 Crm 自己的加密格式生成登录 Cookie（请求头 Cookie 的值），会经过真实的 Cookie 校验和安全戳比对</summary>
    public string CreateCookie(long userId, string scheme = AuthSchemes.Crm, string stamp = FakeSecurityStampValidator.ValidStamp)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"user{userId}"),
            new Claim(SuanpanClaimTypes.SecurityStamp, stamp),
        ], scheme);
        var options = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), new AuthenticationProperties(), scheme);
        return $"{scheme}={options.TicketDataFormat.Protect(ticket)}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);

        // 启动时必填的配置；地址都不会真正连接
        builder.UseSetting("ConnectionStrings:Main", "Host=127.0.0.1;Port=1;Database=not_connected");
        builder.UseSetting("Redis:Configuration", "127.0.0.1:1");
        builder.UseSetting("RabbitMq:Uri", "amqp://127.0.0.1:1/");
        builder.UseSetting("Security:BootstrapAllowedIps", BootstrapIp);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IConfigService>(Config);
            services.AddSingleton<IIpWhitelistService>(Whitelist);
            services.AddSingleton<IPermissionService>(Permissions);
            services.AddSingleton<ISecurityStampValidator>(Stamps);
            services.AddSingleton<IAuthService>(Auth);
            services.Remove(services.Single(d => d.ImplementationType == typeof(AdminSeedHostedService)));
            services.AddSingleton<IOpLogService>(OpLogs);
            services.AddSingleton<IMenuService>(Menus);
            services.AddSingleton<IIndicatorService>(Indicators);
            services.AddSingleton<IDataSourceService>(DataSources);
            services.AddSingleton<ICollectRunService>(CollectRuns);
            services.AddSingleton<ICalendarEventService>(CalendarEvents);
            services.AddSingleton<IUnmatchedEntryService>(Unmatched);

            // Hangfire 面板改用空存储；IGlobalConfiguration 直接给出，不执行连接数据库的存储配置
            services.AddSingleton<IGlobalConfiguration>(GlobalConfiguration.Configuration);
            services.AddSingleton<JobStorage>(new EmptyJobStorage());

            services.AddControllersWithViews().AddApplicationPart(typeof(CrmApp).Assembly);
            services.AddSingleton<IStartupFilter, RemoteIpStartupFilter>();
        });
    }

    /// <summary>TestServer 不设置客户端地址，在管道最前面补上</summary>
    private sealed class RemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                var header = context.Request.Headers[RemoteIpHeader].ToString();
                context.Connection.RemoteIpAddress = header.Length > 0 ? IPAddress.Parse(header) : IPAddress.Loopback;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
