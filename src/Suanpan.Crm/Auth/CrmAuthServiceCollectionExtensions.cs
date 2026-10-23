using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Suanpan.Shared.Auth;

namespace Suanpan.Crm.Auth;

/// <summary>Crm 的认证、授权和反向代理设置（sys 设计 §5、§6）</summary>
public static class CrmAuthServiceCollectionExtensions
{
    public const string LoginPath = "/Account/Login";
    public const string AccessDeniedPath = "/Account/AccessDenied";

    /// <summary>待完成 Cookie 的有效期：固定 10 分钟，不滑动（sys 设计 §5.2）</summary>
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 两个 Cookie：正式登录（空闲超时 Auth:IdleTimeoutMinutes，默认 30 分钟，滑动）和待完成（10 分钟，不滑动）。
    /// 都是 HttpOnly、SameSite=Lax；Development 环境跟随请求（本地 http 也能登录），其他环境一律 Secure（BR-142）。
    /// 默认 scheme 为正式登录，所以待完成 Cookie 访问业务页面会被当作未登录。
    /// 开发环境可以跳过 TOTP 验证码，见 <see cref="TotpSkipSwitch"/>。
    /// </summary>
    public static IServiceCollection AddCrmAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var idleMinutes = configuration.GetValue("Auth:IdleTimeoutMinutes", 30);
        if (idleMinutes <= 0)
        {
            throw new InvalidOperationException("配置 Auth:IdleTimeoutMinutes 必须大于 0");
        }

        var securePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        services.AddAuthentication(AuthSchemes.Crm)
            .AddCookie(AuthSchemes.Crm, options =>
            {
                ConfigureCookie(options, AuthSchemes.Crm, securePolicy);
                options.ExpireTimeSpan = TimeSpan.FromMinutes(idleMinutes);
                options.SlidingExpiration = true;
            })
            .AddCookie(AuthSchemes.CrmPending, options =>
            {
                ConfigureCookie(options, AuthSchemes.CrmPending, securePolicy);
                options.ExpireTimeSpan = PendingLifetime;
                options.SlidingExpiration = false;
            });
        services.AddSingleton(TotpSkipSwitch.From(configuration, environment));

        return services;
    }

    /// <summary>
    /// 动态 Policy（perm:xxx）和默认拒绝：Area 内的 action 由 <see cref="AutoPermissionConvention"/> 加上权限要求；
    /// 其余没有标授权特性的页面（根目录 Controller 等）也必须登录，需要匿名访问的显式标 [AllowAnonymous]。
    /// </summary>
    public static IServiceCollection AddCrmAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddScoped<CurrentPermissions>();

        return services;
    }

    /// <summary>
    /// 反向代理 IP 透传：只采信可信代理（ForwardedHeaders:TrustedProxies，逗号分隔的 IP 或 CIDR）传来的 X-Forwarded-For，
    /// 未配置时只信任本机回环地址。可信代理配错时，IP 白名单和日志里的 IP 都会是代理的地址（sys 设计 §5.5）。
    /// </summary>
    public static IServiceCollection AddCrmForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var trustedProxies = IpListConfig.Parse(configuration, "ForwardedHeaders:TrustedProxies");

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var entry in trustedProxies)
            {
                if (entry.Contains('/'))
                {
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(entry));
                }
                else
                {
                    options.KnownProxies.Add(IPAddress.Parse(entry));
                }
            }
        });

        return services;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options, string name, CookieSecurePolicy securePolicy)
    {
        options.Cookie.Name = name;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = securePolicy;
        options.LoginPath = LoginPath;
        options.AccessDeniedPath = AccessDeniedPath;
        options.Events = CrmCookieEvents.Create();
    }
}
