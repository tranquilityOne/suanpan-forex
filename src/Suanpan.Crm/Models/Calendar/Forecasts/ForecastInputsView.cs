using Suanpan.Core.Reference.Enums;

namespace Suanpan.Crm.Models.Calendar.Forecasts;

/// <summary>
/// 预期值的输入框（清单的行、事件详情共用，局部视图 _ForecastInputs）：区间型两个框，旁边显示单位，step 按指标小数位；
/// 来源说明用 datalist（id 为 forecast-note-options）提示常用来源。预填当前值时先用 EventValueFormat.Round 按小数位取整
/// </summary>
public sealed record ForecastInputsView(
    decimal? Forecast, decimal? ForecastUpper, string? Note, IndicatorValueType ValueType, IndicatorUnit Unit, short Decimals, bool Invalid = false);
