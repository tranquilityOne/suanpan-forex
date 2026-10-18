using Suanpan.Services.Sys.Security;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Security;

public class QrCodeHelperTests
{
    [Fact]
    public void ToPng_ReturnsPngImage()
    {
        var png = QrCodeHelper.ToPng("otpauth://totp/suanpan:admin?secret=GEZDGNBVGY3TQOJQ");

        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(signature, png.Take(8));
    }
}
