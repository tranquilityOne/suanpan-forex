using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Forecasts;
using Suanpan.Services.Calendar.Jobs;
using Suanpan.Services.Calendar.Releases;
using Suanpan.Services.Calendar.Schedules;
using Suanpan.Services.Calendar.Unmatched;
using Suanpan.Services.Collect.Ingest;
using Suanpan.Services.Collect.Jobs;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Collect.Sources;
using Suanpan.Services.Collect.Tasks;
using Suanpan.Services.Common.Alerts;
using Suanpan.Services.Reference.Currencies;
using Suanpan.Services.Reference.Economies;
using Suanpan.Services.Reference.IndicatorSources;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Security;
using Suanpan.Shared.Auth;

namespace Suanpan.Services;

public static class ServicesServiceCollectionExtensions
{
    /// <summary>
    /// 注册业务服务。调用方还需要注册 Data（AddSuanpanData）、Redis（AddSuanpanRedis，登录的 TOTP 防重放、数据源限流等用到）、
    /// 采集用的 HttpClient（AddSuanpanCollectHttp，数据源连通性测试用到）和 RabbitMQ（AddSuanpanEventBus，采集的运行记录下发用到），
    /// 见 docs/conventions/backend.md §4。
    /// 权限树重建 <see cref="IPermissionRebuildService"/> 依赖 MVC、初始管理员的启动服务 <see cref="AdminSeedHostedService"/>，只由 Crm 注册。
    /// </summary>
    public static IServiceCollection AddSuanpanServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        services.AddHttpContextAccessor();
        services.TryAddSingleton<ICurrentUser, HttpContextCurrentUser>();

        // 告警：M1 只写 Error 日志，Ops 模块（FR-025）完成后在此之前注册真正的实现
        services.TryAddSingleton<IAlertService, LoggingAlertService>();

        // Sys：安全组件
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<ISecretProtector, AesSecretProtector>();
        services.AddScoped<IIpWhitelistService, IpWhitelistService>();

        // Sys：认证与授权
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<TotpReplayGuard>();
        services.AddScoped<AdminSeeder>();
        services.AddScoped<ISecurityStampValidator, SecurityStampValidator>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IPresetRoleGrantService, PresetRoleGrantService>();

        // Sys：账号、角色、权限点管理
        services.AddScoped<IUserManageService, UserManageService>();
        services.AddScoped<IRoleManageService, RoleManageService>();
        services.AddScoped<IPermissionManageService, PermissionManageService>();

        // Sys：日志与配置；OpLogActionFilter 由 Crm 注册为全局过滤器
        services.AddScoped<IOpLogService, OpLogService>();
        services.AddScoped<ILoginLogService, LoginLogService>();
        services.AddScoped<OpLogActionFilter>();
        services.AddScoped<IConfigService, ConfigService>();

        // Reference：基础数据
        services.AddScoped<ICurrencyService, CurrencyService>();
        services.AddScoped<IEconomyService, EconomyService>();
        services.AddScoped<IIndicatorService, IndicatorService>();
        services.AddScoped<IIndicatorSourceService, IndicatorSourceService>();

        // Collect：数据采集
        services.AddScoped<IDataSourceService, DataSourceService>();
        services.TryAddSingleton<IParserCatalog, DefaultParserCatalog>();
        services.AddScoped<ICollectTaskService, CollectTaskService>();
        services.AddScoped<ICollectRunService, CollectRunService>();
        services.AddScoped<ISnapshotService, SnapshotService>();
        services.AddScoped<ISnapshotContentStore, PostgresSnapshotContentStore>();
        services.AddScoped<CollectDispatchJob>();
        services.AddScoped<CollectRunCleanupJob>();
        services.AddScoped<CollectSnapshotCleanupJob>();

        // Calendar：财经日历。入库服务注册为 ICollectIngestor，Collector 按目标数据类型取用（采集设计 §5.3）
        services.AddScoped<ScheduleApplier>();
        services.AddScoped<ICollectIngestor, CalendarScheduleIngestor>();
        services.AddScoped<ICollectIngestor, CalendarReleaseIngestor>();
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddScoped<IUnmatchedEntryService, UnmatchedEntryService>();
        services.AddScoped<IForecastService, ForecastService>();
        services.AddScoped<ForecastReminderJob>();

        // 公布值的质量校验：FR-026（Ops）完成前全部通过，FR-026 用 Replace 换成真正的实现（日历设计 §5.6）
        services.TryAddScoped<IReleaseValidator, PassThroughReleaseValidator>();

        return services;
    }
}
