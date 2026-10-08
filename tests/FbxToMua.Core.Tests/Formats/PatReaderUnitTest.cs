using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Pat;
using FbxToMua.Core.Tests.Fakes;

namespace FbxToMua.Core.Tests.Formats;

public class PatReaderUnitTest
{
    [Fact]
    public void Find_SmallSheet_FindsPatternsByNameAndTrimmed()
    {
        // Arrange
        byte[] blob = StageFactory.SmallSheet();

        // Act
        PatDocument sheet = PatReader.Read(blob);

        // Assert
        Assert.Equal(3, sheet.Patterns.Count);
        Assert.Equal(2, sheet.Find("walk0")!.Sprites.Count);
        Assert.NotNull(sheet.Find("walk1"));
        Assert.Null(sheet.Find("run"));
    }

    [Fact]
    public void Read_SmallSheet_ReadsTheSpriteTags()
    {
        // Arrange
        byte[] blob = StageFactory.SmallSheet();

        // Act
        PatSprite sprite = PatReader.Read(blob).Find("walk0")!.Sprites[0];

        // Assert
        Assert.Equal(7, sprite.Id);
        Assert.Equal(100, sprite.X);
        Assert.Equal(-50, sprite.Y);
        Assert.Equal(3, sprite.Part);
        Assert.Equal(10, sprite.Priority);
        Assert.Equal(0xC80A141Eu, sprite.Tint);
        Assert.Equal(0.25f, sprite.Turns);
        Assert.Equal(0.125f, sprite.Pitch);
        Assert.Equal(-0.0625f, sprite.Yaw);
    }

    [Fact]
    public void PartOf_SmallSheet_FindsTheCutOutById()
    {
        // Arrange
        PatDocument sheet = PatReader.Read(StageFactory.SmallSheet());

        // Act
        PatPart? part = sheet.PartOf(3);

        // Assert
        Assert.NotNull(part);
        Assert.Equal((16, 32, 64, 48), (part.U, part.V, part.W, part.H));
        Assert.Equal((128, 96, 64, 90), (part.Width, part.Height, part.PivotX, part.PivotY));
        Assert.Null(sheet.PartOf(4));
    }

    [Fact]
    public void AtlasOf_SmallSheet_IsAWholeDds()
    {
        // Arrange
        byte[] blob = StageFactory.SmallSheet();

        // Act
        PatAtlas? atlas = PatReader.Read(blob).AtlasOf(0);

        // Assert
        Assert.NotNull(atlas);
        Assert.Equal((8, 8), (atlas.Width, atlas.Height));
        Assert.Equal(128 + 8 * 8 * 4, atlas.Dds.Length);
        Assert.True(LittleEndian.Starts(atlas.Dds, "DDS "));
    }

    [Fact]
    public void AtlasOf_Dxt1Sheet_IsAWholeDdsOfHalfAByteAPixel()
    {
        // Arrange
        ByteSink sink = new();
        sink.Text("PAniDataFile");
        sink.PadTo(0x20);
        sink.Text("_STRPGST");
        sink.Dword(4);
        sink.Text("PGNM");
        sink.Zeros(0x20);
        byte[] dds = new byte[128 + 8];
        LittleEndian.Latin1("DDS ").CopyTo(dds, 0);
        Array.Fill(dds, (byte)0x5a, 128, 8);
        sink.Text("PGT2");
        sink.Int(dds.Length);
        sink.Int(4);
        sink.Int(4);
        sink.Text("DXT1");
        sink.Zeros(8);
        sink.Bytes(dds);
        sink.Text("PGED_END");

        // Act
        PatAtlas? atlas = PatReader.Read(sink.ToArray()).AtlasOf(4);

        // Assert
        Assert.NotNull(atlas);
        Assert.Equal((4, 4), (atlas.Width, atlas.Height));
        Assert.Equal(dds, atlas.Dds);
    }
}
