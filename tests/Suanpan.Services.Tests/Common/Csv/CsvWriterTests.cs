using System.Text;
using Suanpan.Services.Common.Csv;
using Xunit;

namespace Suanpan.Services.Tests.Common.Csv;

public class CsvWriterTests
{
    [Fact]
    public async Task WritesBomAndQuotesSpecialCharacters()
    {
        var bytes = await WriteAsync(["名称", "a,b", "say \"hi\"", "two\nlines", null]);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.Equal("名称,\"a,b\",\"say \"\"hi\"\"\",\"two\nlines\",\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Theory]
    [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
    [InlineData("@cmd", "'@cmd")]
    public async Task FormulaLikeCells_ArePrefixed(string cell, string expected)
    {
        var bytes = await WriteAsync([cell]);

        Assert.Equal(expected + "\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    private static async Task<byte[]> WriteAsync(string?[] cells)
    {
        using var output = new MemoryStream();
        await using (var csv = new CsvWriter(output))
        {
            await csv.WriteRowAsync(cells);
        }

        return output.ToArray();
    }
}
