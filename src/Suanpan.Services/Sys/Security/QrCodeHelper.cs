using QRCoder;

namespace Suanpan.Services.Sys.Security;

/// <summary>二维码生成（QRCoder 的 PngByteQRCode，纯托管实现，不依赖 System.Drawing）</summary>
public static class QrCodeHelper
{
    /// <summary>内容转 PNG 字节，视图中用 data:image/png;base64 内嵌</summary>
    public static byte[] ToPng(string content, int pixelsPerModule = 5)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
