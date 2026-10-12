using FbxToMua.Core.Binary;
using FbxToMua.Core.Imaging;
using FbxToMua.Core.Tests.Formats;

namespace FbxToMua.Core.Tests.Imaging;

public class DdsCodecUnitTest
{
    private const uint Required = 0x1 | 0x2 | 0x4 | 0x1000;
    private const int FirstBlock = 128;

    [Fact]
    public void Decode_Dxt3_ReadsTheExplicitAlpha()
    {
        // Arrange
        byte[] dds = DdsHeaderUnitTest.DdsOf(Required, 4, 4, 0, "DXT3", 0);
        dds[FirstBlock] = 0x0f;
        LittleEndian.Put(dds, FirstBlock + 8, 0xf800u);

        // Act
        BgraImage image = DdsCodec.Decode(dds)!;

        // Assert
        Assert.Equal([0x00, 0x00, 0xff, 0xff], image.Pixels[..4]);
        Assert.Equal([0x00, 0x00, 0xff, 0x00], image.Pixels[4..8]);
    }
}
