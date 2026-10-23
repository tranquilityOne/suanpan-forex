namespace Suanpan.Services.Common.References;

/// <summary>
/// 删除被引用的数据时，提示中列出引用方（基础数据 ERR-REF-002、数据源 ERR-SRC-003，基础数据设计 §5.2）：
/// 每类最多列 10 个（编码或名称），其余给出总数。
/// </summary>
public static class ReferenceUsage
{
    private const int MaxListed = 10;

    /// <summary>没有引用时返回 null</summary>
    public static string? DescribeOrNull(params (string Kind, IReadOnlyList<string> Codes)[] usages)
    {
        var parts = usages
            .Where(u => u.Codes.Count > 0)
            .Select(u => u.Codes.Count <= MaxListed
                ? $"{u.Kind} {string.Join("、", u.Codes)}"
                : $"{u.Kind} {string.Join("、", u.Codes.Take(MaxListed))} 等 {u.Codes.Count} 个")
            .ToList();

        return parts.Count == 0 ? null : $"被以下数据引用，不能删除，可以停用：{string.Join("；", parts)}";
    }
}
