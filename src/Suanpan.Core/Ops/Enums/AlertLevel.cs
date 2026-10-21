namespace Suanpan.Core.Ops.Enums;

/// <summary>告警级别（data-hub BR-129）；各级别的送达时限见 BR-131</summary>
public enum AlertLevel : short
{
    /// <summary>紧急</summary>
    Urgent = 1,

    /// <summary>重要</summary>
    Important = 2,

    /// <summary>一般</summary>
    Normal = 3,
}
