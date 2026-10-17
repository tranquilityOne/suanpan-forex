namespace Suanpan.Shared.Auth;

/// <summary>
/// 权限点显示名：打在 action 上（按需），重建时作为按钮级权限的名称。
/// 未标注时用默认名称（见 <see cref="PermCodeHelper.DefaultActionName"/>）。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PermNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
