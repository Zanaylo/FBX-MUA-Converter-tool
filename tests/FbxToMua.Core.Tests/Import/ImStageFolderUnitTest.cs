using System.Text;
using FbxToMua.Core.Import;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Tests.Import;

public class ImStageFolderUnitTest
{
    [Fact]
    public void Renamed_ObjectList_PointsAtTheNumberedFolderAndDropsTheTail()
    {
        // Arrange
        byte[] objects = Encoding.Latin1.GetBytes("BgObject <-\r\n{\r\n\tpanidata = \"./bg/bg_snowtown/bg_snowtown_particles.pat\",\r\n}\r\ntrailing junk");

        // Act
        string renamed = Encoding.Latin1.GetString(ImStageFolder.Renamed(objects, "bg_snowtown", "bg112"));

        // Assert
        Assert.Equal("BgObject <-\r\n{\r\n\tpanidata = \"./bg/bg112/bg_snowtown_particles.pat\",\r\n}", renamed);
    }

    [Theory]
    [InlineData(GameKind.Bbtag, " (BBTAG)")]
    [InlineData(GameKind.Bbcf, " (BBCF)")]
    [InlineData(GameKind.P4u2, " (P4U2)")]
    [InlineData(GameKind.Uni2, "")]
    public void Tag_Game_NamesTheGameTheWayTheModDoes(GameKind game, string expected)
    {
        // Act
        string tag = ImStageFolder.Tag(game);

        // Assert
        Assert.Equal(expected, tag);
    }
}
