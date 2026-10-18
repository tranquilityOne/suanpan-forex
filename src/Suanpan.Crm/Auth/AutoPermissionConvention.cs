using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Suanpan.Shared.Auth;

namespace Suanpan.Crm.Auth;

/// <summary>
/// 默认拒绝（sys 设计 §6.2）：给每个 Area 内的 action 自动加上权限要求 {area}:{controller}:{action}，
/// 忘了标特性的 action 会返回 403，不会裸奔。权限码写进端点元数据的 [HasPermission]，
/// 权限树重建和操作日志读的都是这一份。
/// Controller 或 action 已显式标 [HasPermission]（多个 action 共用一个码、特殊权限码）时不再自动添加。
/// 根目录的 Controller 不进入权限体系，只要求登录（兜底策略）。
/// </summary>
public sealed class AutoPermissionConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        if (!controller.RouteValues.TryGetValue("area", out var area) || string.IsNullOrEmpty(area)
            || controller.Attributes.OfType<HasPermissionAttribute>().Any())
        {
            return;
        }

        foreach (var action in controller.Actions)
        {
            if (action.Attributes.OfType<HasPermissionAttribute>().Any())
            {
                continue;
            }

            var permission = new HasPermissionAttribute(PermCodeHelper.Derive(area, controller.ControllerName, action.ActionName));
            foreach (var selector in action.Selectors)
            {
                selector.EndpointMetadata.Add(permission);
            }
        }
    }
}
