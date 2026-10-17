// 服务注册与请求管道按 docs/conventions/backend.md §4 在后续任务中补齐
var builder = WebApplication.CreateBuilder(args);

// 环境变量 SUANPAN__节__键 的优先级最高，见 docs/conventions/backend.md §12
builder.Configuration.AddEnvironmentVariables(prefix: "SUANPAN__");

var app = builder.Build();

app.Run();
