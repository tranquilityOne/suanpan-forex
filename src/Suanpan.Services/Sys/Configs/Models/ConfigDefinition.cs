using Suanpan.Core.Sys.Enums;

namespace Suanpan.Services.Sys.Configs.Models;

/// <summary>代码中定义的配置项：类型、默认值、页面分组和说明（默认值以字符串保存，格式同 sys_config.config_value）</summary>
public sealed record ConfigDefinition(string Key, ConfigValueType ValueType, string DefaultValue, string Group, string Description);
