namespace Suanpan.Shared.Auth;

/// <summary>
/// 自定义 Claim 类型。用户 id、登录名使用标准的 NameIdentifier、Name；
/// 角色和权限不放进 Claims，每次从权限快照读取（sys 设计 §5.2）。
/// </summary>
public static class SuanpanClaimTypes
{
    /// <summary>姓名（展示用）</summary>
    public const string RealName = "suanpan:real_name";

    /// <summary>安全戳副本：逐请求与数据库比对，不一致即注销（sys 设计 §5.3）</summary>
    public const string SecurityStamp = "suanpan:security_stamp";
}
