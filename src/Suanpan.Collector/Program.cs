using Suanpan.Collector.Execution;
using Suanpan.Data;
using Suanpan.Infrastructure;
using Suanpan.Services;

var builder = WebApplication.CreateBuilder(args);

// 环境变量 SUANPAN__节__键 的优先级最高，见 docs/conventions/backend.md §12
builder.Configuration.AddEnvironmentVariables(prefix: "SUANPAN__");

var services = builder.Services;
var configuration = builder.Configuration;

// 注册组合见 docs/conventions/backend.md §4：Collector 接收 CollectRunCommand 执行采集，重试时延时发给自己
services.AddSuanpanData(configuration, "collector");
services.AddSuanpanServices();
services.AddSuanpanRedis(configuration);
services.AddSuanpanCollectHttp(configuration);
services.AddSuanpanEventBus(configuration, "collector");
services.AddSuanpanCollectors();

var app = builder.Build();

app.Run();
