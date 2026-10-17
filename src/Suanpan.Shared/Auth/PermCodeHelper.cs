using System.Text;

namespace Suanpan.Shared.Auth;

/// <summary>
/// 权限码生成规则（唯一来源）：{area}:{controller}:{action}，三段都是小写 snake_case。
/// 例：Sys / OpLog / Export → sys:op_log:export。
/// AutoPermissionConvention 和权限树重建共用这份规则，两边生成的码不会不一致（sys 设计 §4.4）。
/// </summary>
public static class PermCodeHelper
{
    public static string Derive(string area, string controller, string action)
        => $"{ToSnake(area)}:{ToSnake(controller)}:{ToSnake(action)}";

    /// <summary>
    /// PascalCase → snake_case（OpLog → op_log）。只在"小写或数字后接大写"处断开，
    /// 连续的大写字母不拆分（ABC → abc），所以类名中的缩写按 Pascal 写法命名，如 IpWhitelist。
    /// </summary>
    public static string ToSnake(string pascal)
    {
        var sb = new StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (char.IsUpper(c) && i > 0 && !char.IsUpper(pascal[i - 1]))
            {
                sb.Append('_');
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    /// <summary>action 未标 [PermName] 时的默认显示名</summary>
    public static string DefaultActionName(string action) => action switch
    {
        "Index" => "列表",
        "Detail" => "详情",
        "Create" => "新增",
        "Edit" => "编辑",
        "Delete" => "删除",
        "Save" => "保存",
        "Export" => "导出",
        _ => action,
    };
}
