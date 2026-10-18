using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Sys.Entities;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Logging;

/// <summary>
/// 操作日志过滤器（sys 设计 §7.1）：Crm 全局注册，只处理标了 [OpLog] 的 action。
/// 采集脱敏后的请求参数、权限码、操作人当时的角色、名为 reason 的参数、结果、耗时、IP、TraceId。
/// 日志写入失败不影响业务请求，只记 Error 日志（告警在 ENG-005.4 加入）。
/// </summary>
public sealed class OpLogActionFilter(
    IOpLogService opLogService,
    IPermissionService permissionService,
    ICurrentUser currentUser,
    ILogger<OpLogActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var opLog = context.ActionDescriptor.EndpointMetadata.OfType<OpLogAttribute>().FirstOrDefault();
        if (opLog is null)
        {
            await next();
            return;
        }

        // 参数在执行前采集，执行后可能已被修改
        var requestParams = SensitiveDataMasker.SerializeArguments(context.ActionArguments);
        var reason = ResolveReason(context.ActionArguments);
        var stopwatch = Stopwatch.StartNew();
        var executed = await next();
        stopwatch.Stop();

        try
        {
            var (isSuccess, errorMsg) = ResolveResult(context, executed);
            var http = context.HttpContext;
            var userId = currentUser.UserId;
            var roles = userId is { } id
                ? string.Join(',', (await permissionService.GetSnapshotAsync(id, http.RequestAborted)).RoleCodes)
                : string.Empty;

            await opLogService.AddAsync(new SysOpLog
            {
                UserId = userId ?? 0,
                Username = currentUser.UserName ?? "anonymous",
                Roles = roles,
                Module = opLog.Module,
                Action = opLog.Action,
                PermCode = ResolvePermCode(context),
                HttpMethod = http.Request.Method,
                RequestUrl = http.Request.Path + http.Request.QueryString,
                RequestParams = requestParams,
                Reason = reason,
                IsSuccess = isSuccess,
                ErrorMsg = errorMsg,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                Ip = http.Connection.RemoteIpAddress?.ToString(),
                UserAgent = http.Request.Headers.UserAgent.ToString(),
                TraceId = Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "操作日志写入失败：{Module}/{Action}", opLog.Module, opLog.Action);
        }
    }

    /// <summary>未处理的异常、TempData["Error"]、ModelState 校验失败都算失败</summary>
    private static (bool IsSuccess, string? ErrorMsg) ResolveResult(ActionExecutingContext context, ActionExecutedContext executed)
    {
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return (false, executed.Exception.Message);
        }

        if (context.Controller is Controller controller)
        {
            // Peek 不消费 TempData，页面照常显示提示
            if (controller.TempData.Peek("Error") is string tempError && tempError.Length > 0)
            {
                return (false, tempError);
            }

            if (!controller.ModelState.IsValid)
            {
                var firstError = controller.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrEmpty(m));
                return (false, firstError ?? "参数校验未通过");
            }
        }

        return (true, null);
    }

    /// <summary>从端点元数据中的 [HasPermission] 取权限码（AutoPermissionConvention 自动加上，或显式标注）</summary>
    private static string? ResolvePermCode(ActionExecutingContext context)
    {
        var policy = context.ActionDescriptor.EndpointMetadata.OfType<HasPermissionAttribute>().FirstOrDefault()?.Policy;
        return policy?.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal) == true
            ? policy[HasPermissionAttribute.PolicyPrefix.Length..]
            : null;
    }

    /// <summary>名为 reason 的字符串参数，或参数对象上的 Reason 属性（如有）</summary>
    private static string? ResolveReason(IDictionary<string, object?> arguments)
    {
        foreach (var (name, value) in arguments)
        {
            if (value is string text)
            {
                if (name.Equals("reason", StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }

                continue;
            }

            var property = value?.GetType().GetProperty("Reason", BindingFlags.Public | BindingFlags.Instance);
            if (property?.PropertyType == typeof(string))
            {
                return property.GetValue(value) as string;
            }
        }

        return null;
    }
}
