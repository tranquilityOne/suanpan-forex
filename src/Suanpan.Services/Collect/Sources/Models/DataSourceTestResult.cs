namespace Suanpan.Services.Collect.Sources.Models;

/// <summary>
/// 连通性测试的结果（PRD FR-001 主流程第 2 步）：Success 为 2xx；没发出请求或请求失败时 StatusCode 为空，原因见 Error。
/// Snippet 为响应开头的若干字符，页面按纯文本显示（硬性规则 10）
/// </summary>
public sealed record DataSourceTestResult(bool Success, int? StatusCode, long ElapsedMs, string? Snippet, string? Error)
{
    public static DataSourceTestResult NotSent(string error) => new(false, null, 0, null, error);
}
