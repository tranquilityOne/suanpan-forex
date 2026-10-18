namespace Suanpan.Services.Common.Querying;

/// <summary>
/// 把用户输入的关键字转成 LIKE / ILIKE 的"包含"匹配模式，% 和 _ 按普通字符匹配。
/// 调用 <c>EF.Functions.ILike</c> 时必须传入 <see cref="Escape"/>：不传时 Npgsql 会生成 ESCAPE ''，转义不起作用。
/// </summary>
public static class LikePattern
{
    public const string Escape = @"\";

    public static string Contains(string keyword)
        => "%" + keyword.Trim().Replace(Escape, Escape + Escape).Replace("%", Escape + "%").Replace("_", Escape + "_") + "%";
}
