namespace Suanpan.Shared.Mvc;

/// <summary>
/// 管理操作结果：失败时 Error 为面向用户的提示（各模块的管理服务通用）。
/// 错误针对某个输入字段时，Field 为该字段名（与表单的属性名一致），页面把提示显示在字段旁边；为空时显示在表单顶部。
/// </summary>
public sealed record ManageResult(bool Success, string? Error, string? Field = null)
{
    public static readonly ManageResult Ok = new(true, null);

    public static ManageResult Fail(string error, string? field = null) => new(false, error, field);
}
