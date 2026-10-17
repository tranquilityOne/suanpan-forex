namespace Suanpan.Shared.Auth;

/// <summary>
/// 菜单元数据：打在 Area 内的 Controller 上（必打），声明该控制器在侧边栏的菜单名、图标、排序；
/// 菜单路由由权限树重建自动生成为 /{Area}/{Controller}（sys 设计 §6.2、§6.3）。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PermMenuAttribute(string name) : Attribute
{
    /// <summary>菜单显示名（中文）</summary>
    public string Name { get; } = name;

    /// <summary>Tabler Icons 名称（渲染为 &lt;i class="ti ti-{icon}"&gt;）</summary>
    public string? Icon { get; set; }

    /// <summary>同目录内排序</summary>
    public int Sort { get; set; }
}
