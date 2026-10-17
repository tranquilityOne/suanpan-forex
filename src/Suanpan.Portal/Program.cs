// 服务注册与请求管道按 docs/conventions/backend.md §4 在后续任务中补齐
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Run();
