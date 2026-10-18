namespace Suanpan.Core.Sys.Constants;

/// <summary>预置角色的编码（sys 设计 §3.4），由第一个迁移写入 sys_role。</summary>
public static class RoleCodes
{
    /// <summary>管理员：内置角色，授权时直接放行，不可修改、删除和授权</summary>
    public const string SuperAdmin = "super_admin";

    /// <summary>采集工程师</summary>
    public const string Engineer = "engineer";

    /// <summary>编辑</summary>
    public const string Editor = "editor";

    /// <summary>只读</summary>
    public const string Viewer = "viewer";
}
