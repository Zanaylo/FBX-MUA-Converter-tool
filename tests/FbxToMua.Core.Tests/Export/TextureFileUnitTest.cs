using FbxToMua.Core.Export;

namespace FbxToMua.Core.Tests.Export;

public class TextureFileUnitTest
{
    [Fact]
    public void Candidates_DdsName_IsJustItself()
    {
        // Arrange
        const string leaf = "floor.dds";

        // Act
        List<string> candidates = TextureFile.Candidates(leaf);

        // Assert
        Assert.Equal(["floor.dds"], candidates);
    }

    [Fact]
    public void Candidates_SourceArtName_TriesTheDdsFirst()
    {
        // Arrange
        const string leaf = "church2_zimen_AlbedoTransparency.png";

        // Act
        List<string> candidates = TextureFile.Candidates(leaf);

        // Assert
        Assert.Equal(["church2_zimen_AlbedoTransparency.dds", "church2_zimen_AlbedoTransparency.png"], candidates);
    }

    [Fact]
    public void Candidates_NameWithSpaces_AlsoTriesItWithout()
    {
        // Arrange
        const string leaf = "carpet light_2D_View.dds";

        // Act
        List<string> candidates = TextureFile.Candidates(leaf);

        // Assert
        Assert.Equal(["carpet light_2D_View.dds", "carpetlight_2D_View.dds"], candidates);
    }
}
