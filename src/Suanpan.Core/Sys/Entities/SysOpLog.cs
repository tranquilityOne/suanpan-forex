namespace Suanpan.Core.Sys.Entities;

/// <summary>
/// 操作日志（sys_op_log）：只追加，应用账号没有 UPDATE、DELETE 权限，不带审计四件套。
/// 由 OpLogActionFilter 自动写入，业务代码不手工写（sys 设计 §7.1）。
/// </summary>
public class SysOpLog
{
    public long Id { get; set; }

    public long UserId { get; set; }

    /// <summary>登录名快照，账号停用后日志仍可读</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>操作当时的角色编码，逗号分隔；没有角色时为空串</summary>
    public string Roles { get; set; } = string.Empty;

    /// <summary>[OpLog] 的模块名</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>[OpLog] 的动作名</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>从 [HasPermission] 元数据自动采集的权限码</summary>
    public string? PermCode { get; set; }

    public string HttpMethod { get; set; } = string.Empty;

    public string RequestUrl { get; set; } = string.Empty;

    /// <summary>序列化后的请求参数，已脱敏</summary>
    public string? RequestParams { get; set; }

    /// <summary>请求参数中名为 reason 的字段（如有）</summary>
    public string? Reason { get; set; }

    /// <summary>未处理的异常、TempData["Error"]、ModelState 校验失败都记为失败</summary>
    public bool IsSuccess { get; set; } = true;

    public string? ErrorMsg { get; set; }

    public int DurationMs { get; set; }

    public string? Ip { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>用于关联应用日志</summary>
    public string? TraceId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>哈希链（NFR-041，P1），第一阶段先建列、暂不填写</summary>
    public string? PrevHash { get; set; }
}
