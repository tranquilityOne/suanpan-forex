namespace Suanpan.Shared.Mvc;

/// <summary>
/// 操作日志标记，打在需要留痕的 action 上：[OpLog("角色管理", "角色授权")]。
/// 由 OpLogActionFilter 自动采集参数、结果、耗时、IP，业务代码不写日志逻辑。
/// 所有写操作（新增、修改、删除、状态变更、授权、重置、解锁、重建、导出、手动触发）都要标，
/// 查询类 action 不标（sys 设计 §7.1）。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OpLogAttribute(string module, string action) : Attribute
{
    /// <summary>模块名，如 "角色管理"</summary>
    public string Module { get; } = module;

    /// <summary>动作名，如 "角色授权"</summary>
    public string Action { get; } = action;
}
