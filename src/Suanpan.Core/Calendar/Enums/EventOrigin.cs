namespace Suanpan.Core.Calendar.Enums;

/// <summary>日历事件从哪里来（日历设计 §2.1）；排期只来自数据源（DEC-024），没有人工新增和批量导入</summary>
public enum EventOrigin : short
{
    /// <summary>排期采集（FR-007）</summary>
    Collected = 1,

    /// <summary>补建：采到公布值却找不到事件（ERR-EVT-006、BR-157）</summary>
    Supplemented = 2,
}
