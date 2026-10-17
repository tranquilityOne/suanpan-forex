namespace Suanpan.Core.Sys.Enums;

/// <summary>系统配置值的类型：决定后台的编辑控件，保存时按类型校验</summary>
public enum ConfigValueType : short
{
    String = 1,

    Int = 2,

    /// <summary>精确小数（decimal），不用 float、double</summary>
    Decimal = 3,

    /// <summary>布尔开关：true / false</summary>
    Bool = 4,

    /// <summary>JSON 对象或数组</summary>
    Json = 5,
}
