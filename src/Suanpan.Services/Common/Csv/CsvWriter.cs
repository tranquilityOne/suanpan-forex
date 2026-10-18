using System.Text;

namespace Suanpan.Services.Common.Csv;

/// <summary>
/// 写 CSV（RFC 4180）：UTF-8 带 BOM，Excel 打开中文不乱码（前端约定 §7.2）。
/// 含逗号、引号、换行的单元格加引号；以 = + - @ 或制表符开头的单元格前加单引号，
/// 防止导出内容在 Excel 中被当作公式执行（CSV 注入）。
/// </summary>
public sealed class CsvWriter(Stream output) : IAsyncDisposable
{
    private readonly StreamWriter _writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true)
    {
        NewLine = "\r\n",
    };

    public Task WriteRowAsync(params IEnumerable<string?> cells)
        => _writer.WriteLineAsync(string.Join(',', cells.Select(Escape)));

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync();
        await _writer.DisposeAsync();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
