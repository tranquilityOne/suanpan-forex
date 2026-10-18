using Microsoft.Net.Http.Headers;

namespace Suanpan.Crm.Mvc;

/// <summary>
/// CSV 下载的响应头（前端约定 §7.2）：边查边写到响应流，不在内存里攒整个文件。
/// 中文文件名放在 filename*，旧客户端退回到纯 ASCII 的 filename。
/// </summary>
public static class CsvDownload
{
    public static void Begin(HttpResponse response, string fileName, string asciiFallback)
    {
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = asciiFallback,
            FileNameStar = fileName,
        }.ToString();
    }
}
