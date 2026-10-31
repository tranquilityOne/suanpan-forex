using Suanpan.Core.Collect.Entities;

namespace Suanpan.Crm.Models.Collect;

/// <summary>
/// 运行详情的请求记录：一路盯盘每隔几秒请求一次，记录很多，默认只列内容有变化或失败的请求和首末各 5 条，可展开全部
/// （采集设计 §8.1、§11 第 29 项）；其他触发方式全部列出
/// </summary>
public static class CollectAttemptsView
{
    private const int KeepEnds = 5;

    /// <summary>按序号排列的要显示的请求；collapse 为否时全部返回</summary>
    public static IReadOnlyList<CollectAttempt> Visible(IEnumerable<CollectAttempt> attempts, bool collapse)
    {
        var ordered = attempts.OrderBy(a => a.No).ToList();
        return collapse
            ? ordered.Where((a, i) => i < KeepEnds || i >= ordered.Count - KeepEnds || a.Changed == true || IsFailed(a)).ToList()
            : ordered;
    }

    private static bool IsFailed(CollectAttempt attempt) => attempt.Error is not null || attempt.HttpStatus is null or < 200 or >= 300;
}
