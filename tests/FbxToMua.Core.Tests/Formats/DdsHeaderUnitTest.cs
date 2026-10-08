using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Dds;

namespace FbxToMua.Core.Tests.Formats;

public class DdsHeaderUnitTest
{
    private const uint Required = 0x1 | 0x2 | 0x4 | 0x1000;
    private const uint Pitched = 0x8;
    private const uint LinearSize = 0x80000;

    public static byte[] DdsOf(uint flags, uint width, uint height, uint pitch, string? fourCc, uint bits)
    {
        byte[] dds = new byte[128 + 64];
        LittleEndian.Latin1("DDS ").CopyTo(dds, 0);
        LittleEndian.Put(dds, 4, 124);
        LittleEndian.Put(dds, 8, flags);
        LittleEndian.Put(dds, 12, height);
        LittleEndian.Put(dds, 16, width);
        LittleEndian.Put(dds, 20, pitch);
        LittleEndian.Put(dds, 76, 32);
        LittleEndian.Put(dds, 80, fourCc is null ? 0x40u : 0x4u);

        if (fourCc is not null)
            LittleEndian.Latin1(fourCc).CopyTo(dds, 84);

        LittleEndian.Put(dds, 88, bits);

        return dds;
    }

    [Fact]
    public void FirstLevelBytes_Dxt5WithoutLinearSize_IsEmpty()
    {
        // Arrange
        byte[] atlas = DdsOf(Required, 8, 8, 0, "DXT5", 0);

        // Act
        uint bytes = DdsHeader.FirstLevelBytes(atlas);

        // Assert
        Assert.Equal(0u, bytes);
    }

    [Fact]
    public void StateLinearSize_Dxt5_StatesItsFourBlocksAndTheFlag()
    {
        // Arrange
        byte[] atlas = DdsOf(Required, 8, 8, 0, "DXT5", 0);

        // Act
        DdsHeader.StateLinearSize(atlas);

        // Assert
        Assert.Equal(64u, DdsHeader.FirstLevelBytes(atlas));
        Assert.NotEqual(0u, LittleEndian.U32(atlas, 8) & LinearSize);
    }

    [Fact]
    public void StateLinearSize_OddDxt1_RoundsUpToWholeBlocks()
    {
        // Arrange
        byte[] odd = DdsOf(Required, 6, 6, 0, "DXT1", 0);

        // Act
        DdsHeader.StateLinearSize(odd);

        // Assert
        Assert.Equal(32u, DdsHeader.FirstLevelBytes(odd));
    }

    [Fact]
    public void StateLinearSize_StatedHeader_IsLeftAlone()
    {
        // Arrange
        byte[] stated = DdsOf(Required | LinearSize, 8, 8, 32, "DXT1", 0);
        byte[] kept = (byte[])stated.Clone();

        // Act
        DdsHeader.StateLinearSize(kept);

        // Assert
        Assert.Equal(stated, kept);
    }

    [Fact]
    public void StateLinearSize_PitchedSurface_IsLeftAloneAndReadAsPitchTimesHeight()
    {
        // Arrange
        byte[] pitched = DdsOf(Required | Pitched, 8, 8, 32, null, 32);
        byte[] plain = (byte[])pitched.Clone();

        // Act
        DdsHeader.StateLinearSize(plain);

        // Assert
        Assert.Equal(pitched, plain);
        Assert.Equal(256u, DdsHeader.FirstLevelBytes(plain));
    }

    [Fact]
    public void StateLinearSize_NotADds_IsLeftAlone()
    {
        // Arrange
        byte[] foreign = Enumerable.Repeat((byte)7, 200).ToArray();

        // Act
        DdsHeader.StateLinearSize(foreign);

        // Assert
        Assert.All(foreign, value => Assert.Equal(7, value));
        Assert.Equal(0u, DdsHeader.FirstLevelBytes(foreign));
    }
}
