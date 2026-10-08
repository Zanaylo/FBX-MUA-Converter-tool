using FbxToMua.Core.Sources;
using FbxToMua.Core.Sources.Tables;

namespace FbxToMua.Core.Tests.Sources;

public class EnglishStageNamesUnitTest
{
    [Fact]
    public void Of_Uni2Folder_GivesTheShippedEnglishName()
    {
        // Arrange
        string folder = "bg001";

        // Act
        string? name = EnglishStageNames.Of(GameKind.Uni2, folder);

        // Assert
        Assert.Equal("Metropolitan Center: Intersection", name);
    }

    [Fact]
    public void Of_SameFolderInAnotherGame_GivesThatGamesName()
    {
        // Arrange
        string folder = "bg001";

        // Act
        string? name = EnglishStageNames.Of(GameKind.Mbtl, folder);

        // Assert
        Assert.Equal("Gathering of Old Blood", name);
    }

    [Fact]
    public void Of_FolderInCapitals_IsFound()
    {
        // Arrange
        string folder = "BG01";

        // Act
        string? name = EnglishStageNames.Of(GameKind.Mbaa, folder);

        // Assert
        Assert.Equal("Artificial Eden", name);
    }

    [Fact]
    public void Of_UnlistedFolder_IsNull()
    {
        // Arrange
        string folder = "bg65535";

        // Act
        string? name = EnglishStageNames.Of(GameKind.Uni2, folder);

        // Assert
        Assert.Null(name);
    }
}
