using Hangfire;
using Suanpan.Data;
using Suanpan.Infrastructure;
using Suanpan.Job.Registry;
using Suanpan.Services;

var builder = WebApplication.CreateBuilder(args);

// 环境变量 SUANPAN__节__键 的优先级最高，见 docs/conventions/backend.md §12
builder.Configuration.AddEnvironmentVariables(prefix: "SUANPAN__");

var services = builder.Services;
var configuration = builder.Configuration;

// 注册组合见 docs/conventions/backend.md §4：Job 运行 Hangfire Server 执行周期任务，经 RabbitMQ 发布和接收消息
services.AddSuanpanData(configuration, "job");
services.AddSuanpanServices();
services.AddSuanpanRedis(configuration);
services.AddSuanpanCollectHttp(configuration);
services.AddSuanpanEventBus(configuration, "job");
services.AddSuanpanHangfire(configuration, runServer: true);

var app = builder.Build();

app.Services.GetRequiredService<IRecurringJobManager>().RegisterRecurringJobs();

app.Run();
