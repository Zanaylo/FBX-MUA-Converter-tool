using FbxToMua.Core.Preview;

namespace FbxToMua.Core.Tests.Preview;

public class ShadowNoteUnitTest
{
    private static readonly int[] ShippedLight = [0, 900, -1000];
    private static readonly int[] LeaningLight = [-326, 690, -946];

    [Fact]
    public void Of_NoFloorLit_SaysTheGameDrawsNone()
    {
        // Arrange
        int[] light = ShippedLight;

        // Act
        string note = ShadowNote.Of(light, 0);

        // Assert
        Assert.Equal("No floor of this stage takes the shadow with this light, so BBTAG draws none.", note);
    }

    [Fact]
    public void Of_ShippedLight_SaysTheWholeFloorIsShaded()
    {
        // Arrange
        int[] light = ShippedLight;

        // Act
        string note = ShadowNote.Of(light, 1);

        // Assert
        Assert.Equal("BBTAG shades the whole floor with this light.", note);
    }

    [Fact]
    public void Of_LeaningLight_NamesTheStripTheFloorIsCutTo()
    {
        // Arrange
        int[] light = LeaningLight;

        // Act
        string note = ShadowNote.Of(light, 1);

        // Assert
        Assert.StartsWith("BBTAG only shades the floor between z -191 and 191 with this light", note);
    }
}
