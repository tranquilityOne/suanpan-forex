namespace Suanpan.Core.Common.Auditing;

/// <summary>
/// 带审计四件套（created_by、created_at、updated_by、updated_at）的实体基类。
/// 新增时修改字段为空，之后每次修改时由审计拦截器填充。
/// </summary>
public abstract class AuditedEntity : CreationAuditedEntity
{
    /// <summary>最后修改人，取值规则同 <see cref="CreationAuditedEntity.CreatedBy"/>；从未修改过时为空。</summary>
    public string? UpdatedBy { get; private set; }

    /// <summary>最后修改时间（UTC）；从未修改过时为空。</summary>
    public DateTime? UpdatedAt { get; private set; }
}
