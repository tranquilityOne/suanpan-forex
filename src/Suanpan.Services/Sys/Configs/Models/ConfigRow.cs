using Suanpan.Core.Sys.Enums;

namespace Suanpan.Services.Sys.Configs.Models;

/// <summary>
/// 系统配置页面的一行：代码中定义的配置项，合并库中的值。
/// Value 为空表示库里没有值、正在使用默认值；IsDefined 为 false 表示库里有、代码中已不再定义的配置项。
/// </summary>
public sealed record ConfigRow(
    string Key,
    string Group,
    ConfigValueType ValueType,
    string? Description,
    string? DefaultValue,
    string? Value,
    bool IsDefined);
