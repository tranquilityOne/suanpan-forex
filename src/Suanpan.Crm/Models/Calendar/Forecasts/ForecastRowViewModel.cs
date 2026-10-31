using Suanpan.Services.Calendar.Events.Models;

namespace Suanpan.Crm.Models.Calendar.Forecasts;

/// <summary>
/// 清单中的一行（日历设计 §9.4）：保存后只刷新这一行（HTMX，BR-059）。保存成功的行显示"已录入"并聚焦下一行；
/// 出错的行保留填写的值和提交时的 row_version（被他人改过时要刷新页面才能再保存，ERR-FC-003），聚焦自己的输入框
/// </summary>
public sealed class ForecastRowViewModel
{
    public required EventRow Row { get; init; }

    public decimal? Forecast { get; init; }

    public decimal? ForecastUpper { get; init; }

    public string? Note { get; init; }

    public int RowVersion { get; init; }

    public bool Saved { get; init; }

    public string? Error { get; init; }

    public static ForecastRowViewModel Pending(EventRow row) => new()
    {
        Row = row,
        Forecast = EventValueFormat.Round(row.Event.Forecast, row.Decimals),
        ForecastUpper = EventValueFormat.Round(row.Event.ForecastUpper, row.Decimals),
        Note = row.Event.ForecastNote,
        RowVersion = row.Event.RowVersion,
    };

    public static ForecastRowViewModel Done(EventRow row) => new() { Row = row, RowVersion = row.Event.RowVersion, Saved = true };

    public static ForecastRowViewModel Failed(EventRow row, ForecastFormModel form, string error) => new()
    {
        Row = row,
        Forecast = form.Forecast,
        ForecastUpper = form.ForecastUpper,
        Note = form.Note,
        RowVersion = form.RowVersion,
        Error = error,
    };
}
