namespace Suanpan.Core.Common.Auditing;

/// <summary>
/// 带创建字段（created_by、created_at）的实体基类。
/// 字段由数据层的审计拦截器在保存时填充，业务代码不赋值（docs/conventions/backend.md §6.1）。
/// </summary>
public abstract class CreationAuditedEntity
{
    /// <summary>创建人：后台账号的登录名；程序自动写入时为程序名，如 collector、job。</summary>
    public string CreatedBy { get; private set; } = string.Empty;

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; private set; }
}
