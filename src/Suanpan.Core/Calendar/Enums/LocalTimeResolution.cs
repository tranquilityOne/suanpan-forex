namespace Suanpan.Core.Calendar.Enums;

/// <summary>当地时间换算成 UTC 时，这个当地时刻属于哪种情况（BR-037）；后两种只在事件上标记，不改变状态、不需要人工复核</summary>
public enum LocalTimeResolution : short
{
    /// <summary>正常：这个当地时刻只出现一次</summary>
    Exact = 1,

    /// <summary>夏令时开始当天，这个当地时刻不存在，已按跳过的时长顺延</summary>
    ShiftedForward = 2,

    /// <summary>夏令时结束当天，这个当地时刻出现两次，取了较早的一次（按夏令时计）</summary>
    Ambiguous = 3,
}
