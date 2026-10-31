namespace Suanpan.Services.Calendar.Events.Models;

/// <summary>
/// 人工修改的数值（日历设计 §3.7）：只改与当前值不同的字段，为空表示不改（不能清空）。
/// 待复核的事件填了公布值即为"改为人工值"，与原公布值相同也照样写入并锁定（"保留原值"，BR-051）
/// </summary>
public sealed record EventValuesInput(
    decimal? Actual = null,
    decimal? ActualUpper = null,
    decimal? Previous = null,
    decimal? PreviousUpper = null,
    decimal? PreviousRevised = null);
