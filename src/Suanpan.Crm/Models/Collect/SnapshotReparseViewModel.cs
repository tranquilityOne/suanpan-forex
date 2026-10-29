using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Suanpan.Crm.Models.Collect;

/// <summary>
/// 重新解析的表单页（采集设计 §5.6、§8.1）：范围为快照列表的筛选条件加勾选的快照（没有勾选时为全部筛选结果），
/// 选择解析器后二次确认（提示快照数）。筛选条件和勾选的快照放在隐藏字段中提交
/// </summary>
public sealed class SnapshotReparseViewModel
{
    public SnapshotFilter Filter { get; set; } = new();

    /// <summary>勾选的快照；为空表示全部筛选结果</summary>
    public List<long> Ids { get; set; } = [];

    [Display(Name = "解析器")]
    [Required]
    public string? ParserKey { get; set; }

    [BindNever]
    public string? TaskName { get; set; }

    /// <summary>会处理的快照数</summary>
    [BindNever]
    public int Count { get; set; }

    /// <summary>不能发起时的原因（数量为 0 或超过上限）</summary>
    [BindNever]
    public string? Blocked { get; set; }

    /// <summary>同一适配器、目标数据类型相同的解析器</summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> ParserOptions { get; set; } = [];
}
