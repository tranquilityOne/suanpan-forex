using Suanpan.Core.Reference.Entities;

namespace Suanpan.Crm.Models.Reference;

public sealed class IndicatorDetailViewModel
{
    public required Indicator Indicator { get; init; }

    /// <summary>所属经济体；查不到时只显示编码</summary>
    public Economy? Economy { get; init; }
}
