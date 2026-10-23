namespace Suanpan.Core.Reference.Enums;

/// <summary>指标的发布频率</summary>
public enum ReleaseFrequency : short
{
    /// <summary>每周</summary>
    Weekly = 1,

    /// <summary>每月</summary>
    Monthly = 2,

    /// <summary>每季</summary>
    Quarterly = 3,

    /// <summary>不定期，如央行议息</summary>
    Irregular = 4,
}
