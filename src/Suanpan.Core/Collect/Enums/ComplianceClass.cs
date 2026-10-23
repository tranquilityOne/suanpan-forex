namespace Suanpan.Core.Collect.Enums;

/// <summary>数据源的合规类别（BR-002）："待确认"的数据源不能启用</summary>
public enum ComplianceClass : short
{
    /// <summary>政府公开数据</summary>
    GovernmentPublic = 1,

    /// <summary>机构许可</summary>
    InstitutionLicense = 2,

    /// <summary>待确认</summary>
    Pending = 3,
}
