using Suanpan.Core.Common.Auditing;
using Suanpan.Core.Sys.Enums;

namespace Suanpan.Core.Sys.Entities;

/// <summary>
/// 系统配置（sys_config）：业务动态开关，值统一存字符串，用 <see cref="ValueType"/> 标注实际类型。
/// 键不存在时用代码默认值，表里有值时才覆盖；基础设施配置不放这里（后端约定 §12）。
/// </summary>
public class SysConfig : AuditedEntity
{
    public int Id { get; set; }

    /// <summary>配置键，唯一，格式 模块:键名，如 security:login_max_failures</summary>
    public string ConfigKey { get; set; } = string.Empty;

    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>值的类型，保存时按类型校验</summary>
    public ConfigValueType ValueType { get; set; } = ConfigValueType.String;

    /// <summary>页面分组展示用</summary>
    public string ConfigGroup { get; set; } = string.Empty;

    public string? Description { get; set; }
}
