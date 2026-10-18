using Hangfire;
using Suanpan.Crm.Areas.Sys.Controllers;
using Suanpan.Crm.Auth;
using Suanpan.Crm.Mvc;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Infrastructure;
using Suanpan.Services;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Shared.Auth;

var builder = WebApplication.CreateBuilder(args);

// 环境变量 SUANPAN__节__键 的优先级最高，见 docs/conventions/backend.md §12
builder.Configuration.AddEnvironmentVariables(prefix: "SUANPAN__");

var services = builder.Services;
var configuration = builder.Configuration;

// 注册组合见 docs/conventions/backend.md §4。审计字段写登录名，必须在 AddSuanpanData 之前注册
services.AddScoped<IAuditActorProvider, CurrentUserAuditActorProvider>();
services.AddSuanpanData(configuration, "crm");
services.AddSuanpanServices();
services.AddSuanpanRedis(configuration);
services.AddSuanpanEventBus(configuration, "crm");
services.AddSuanpanHangfire(configuration, runServer: false);

services.AddCrmForwardedHeaders(configuration);
services.AddCrmAuthentication(configuration, builder.Environment);
services.AddCrmAuthorization();
services.AddScoped<IPermissionRebuildService, PermissionRebuildService>();
services.AddHostedService<AdminSeedHostedService>();
services.AddSingleton<IOperatorTimeZone, DefaultOperatorTimeZone>();
services.AddControllersWithViews(options =>
{
    options.Conventions.Add(new AutoPermissionConvention());
    options.Filters.AddService<OpLogActionFilter>();
    options.UseChineseValidationMessages();
});

var app = builder.Build();

// 必须最前：之后的 IP 白名单、操作日志取到的才是真实客户端 IP
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// 在路由和认证之前拦截所有请求，包括登录页和静态资源
app.UseMiddleware<IpWhitelistMiddleware>();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

// Hangfire 面板：由端点授权保护，未登录跳转登录页，没有 sys:job:index 跳转无权限页；
// 面板自带的授权过滤器（默认只允许本机访问）清空，不重复判断
app.MapHangfireDashboard(JobController.DashboardPath, new DashboardOptions { AppPath = "/", DashboardTitle = "任务面板", Authorization = [] })
    .RequireAuthorization(new HasPermissionAttribute("sys:job:index"));

app.MapControllerRoute("areas", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();
