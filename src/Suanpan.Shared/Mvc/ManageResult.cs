namespace Suanpan.Shared.Mvc;

/// <summary>管理操作结果：失败时 Error 为面向用户的提示（各模块的管理服务通用）</summary>
public sealed record ManageResult(bool Success, string? Error)
{
    public static readonly ManageResult Ok = new(true, null);

    public static ManageResult Fail(string error) => new(false, error);
}
