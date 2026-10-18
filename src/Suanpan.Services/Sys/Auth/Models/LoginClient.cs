namespace Suanpan.Services.Sys.Auth.Models;

/// <summary>请求来源，写入登录日志</summary>
public sealed record LoginClient(string? Ip, string? UserAgent);
