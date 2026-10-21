using Suanpan.Core.Ops.Enums;

namespace Suanpan.Services.Common.Alerts;

/// <summary>
/// 告警接口（data-hub EV-015：级别、对象、内容）。业务代码发现异常时调用，不关心发到哪里。
/// 渠道、接收人、30 分钟内同一问题只告警一次等规则由 Ops 模块的实现负责（FR-025，M2）；
/// M1 阶段的实现 <see cref="LoggingAlertService"/> 只写 Error 级日志。
/// </summary>
public interface IAlertService
{
    /// <param name="level">告警级别</param>
    /// <param name="subject">告警对象，如"操作日志"、某个数据源</param>
    /// <param name="message">告警内容</param>
    Task RaiseAsync(AlertLevel level, string subject, string message, CancellationToken ct = default);
}
