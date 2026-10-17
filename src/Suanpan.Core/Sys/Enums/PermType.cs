namespace Suanpan.Core.Sys.Enums;

/// <summary>权限点类型（菜单即权限：目录、菜单渲染侧边栏，按钮控制操作级权限）</summary>
public enum PermType : short
{
    /// <summary>目录：对应 Area，侧边栏分组，没有路由</summary>
    Directory = 1,

    /// <summary>菜单：对应 Controller，有路由页面</summary>
    Menu = 2,

    /// <summary>按钮：对应 Action，只是权限点</summary>
    Button = 3,
}
