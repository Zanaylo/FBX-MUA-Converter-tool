using FbxToMua.Core.Imaging;

namespace FbxToMua.Core.Tests.Imaging;

public class ImageOpsUnitTest
{
    private const uint OpaqueRed = 0xffff0000;
    private const uint ClearBlack = 0x00000000;

    [Fact]
    public void Halved_EvenImage_HalvesBothSides()
    {
        // Arrange
        BgraImage image = ImageOps.Blank(8, 4, OpaqueRed);

        // Act
        BgraImage halved = ImageOps.Halved(image);

        // Assert
        Assert.Equal((4, 2), (halved.Width, halved.Height));
        Assert.Equal([0x00, 0x00, 0xff, 0xff], halved.Pixels[..4]);
    }

    [Fact]
    public void Halved_ClearNeighbours_KeepTheColourAndAverageTheAlpha()
    {
        // Arrange
        BgraImage image = ImageOps.Blank(2, 2, ClearBlack);
        ImageOps.Fill(image, new PixelRect(0, 0, 1, 1), OpaqueRed);

        // Act
        BgraImage halved = ImageOps.Halved(image);

        // Assert
        Assert.Equal([0x00, 0x00, 0xff, 0x40], halved.Pixels);
    }

    [Fact]
    public void Halved_OnePixelSide_StaysOnePixel()
    {
        // Arrange
        BgraImage image = ImageOps.Blank(4, 1, OpaqueRed);

        // Act
        BgraImage halved = ImageOps.Halved(image);

        // Assert
        Assert.Equal((2, 1), (halved.Width, halved.Height));
    }
}
