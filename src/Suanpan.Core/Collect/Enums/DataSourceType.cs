namespace Suanpan.Core.Collect.Enums;

/// <summary>数据源类型（BR-001）；中文显示名见 CollectDisplayNames</summary>
public enum DataSourceType : short
{
    /// <summary>官方 API</summary>
    OfficialApi = 1,

    /// <summary>官方网页</summary>
    OfficialWeb = 2,

    /// <summary>RSS / Atom</summary>
    Feed = 3,

    /// <summary>文件下载，如 CSV、XLSX</summary>
    FileDownload = 4,
}
