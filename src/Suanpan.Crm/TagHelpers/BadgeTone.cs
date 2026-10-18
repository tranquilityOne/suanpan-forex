namespace Suanpan.Crm.TagHelpers;

/// <summary>状态徽章的语义颜色（前端约定 §7.1）</summary>
public enum BadgeTone
{
    /// <summary>成功、完成：已公布、已发布、成功、启用</summary>
    Success,

    /// <summary>进行中：盯盘中、运行中</summary>
    InProgress,

    /// <summary>需要关注：待复核、待审、待确认、部分成功</summary>
    Attention,

    /// <summary>异常：延迟、限流</summary>
    Abnormal,

    /// <summary>失败、终止：失败、已撤回、已拒绝、已下架</summary>
    Failure,

    /// <summary>中性：待公布、取消、停用</summary>
    Neutral,
}
