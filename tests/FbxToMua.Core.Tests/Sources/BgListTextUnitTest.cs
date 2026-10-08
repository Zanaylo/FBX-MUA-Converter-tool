using FbxToMua.Core.Export;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Tests.Sources;

public class BgListTextUnitTest
{
    private const string List =
        "BgList <-\r\n{\r\n" +
        "\tBg_001 =\r\n\t{\r\n\t\tName = \"Town\",\r\n\t\tDataFile = \"bg001\",\r\n\t\tScale = [ 1.5, 2.0, 2.5 ],\r\n\t\tViewRotationX = 3.0, // tilt\r\n\t}\r\n" +
        "\tBg_002 =\r\n\t{\r\n\t\tName = \"Park\",\r\n\t\tDataFile = \"bg_park\",\r\n\t\tPosition =\r\n\t\t[ 0.1, 0.2, 0.3 ],\r\n\t}\r\n" +
        "}\r\n";

    [Fact]
    public void Block_DataFileName_FindsItsBlock()
    {
        // Arrange
        string stage = "bg_park";

        // Act
        string? block = BgListText.Block(List, stage);

        // Assert
        Assert.NotNull(block);
        Assert.Equal("\"Park\"", BgListText.Field(block, "Name"));
    }

    [Fact]
    public void Block_NumberedFolder_FallsBackToTheEntryNumber()
    {
        // Arrange
        string stage = "bg001";

        // Act
        string? block = BgListText.Block(List, stage);

        // Assert
        Assert.NotNull(block);
        Assert.Equal("\"Town\"", BgListText.Field(block, "Name"));
    }

    [Fact]
    public void Block_UnknownStage_IsNull()
    {
        // Arrange
        string stage = "bg_nowhere";

        // Act
        string? block = BgListText.Block(List, stage);

        // Assert
        Assert.Null(block);
    }

    [Fact]
    public void Field_ArrayOnTheNextLine_IsReadWhole()
    {
        // Arrange
        string block = BgListText.Block(List, "bg_park")!;

        // Act
        string? position = BgListText.Field(block, "Position");

        // Assert
        Assert.Equal("[ 0.1, 0.2, 0.3 ]", position);
    }

    [Fact]
    public void Field_ValueBeforeAComment_StopsAtTheComment()
    {
        // Arrange
        string block = BgListText.Block(List, "bg001")!;

        // Act
        string? tilt = BgListText.Field(block, "ViewRotationX");

        // Assert
        Assert.Equal("3.0", tilt);
    }

    [Fact]
    public void FramingOf_Block_ReadsScaleAndTilt()
    {
        // Arrange
        string stage = "bg001";

        // Act
        Framing framing = StageSources.FramingOf(List, stage);

        // Assert
        Assert.Equal((1.5f, 2.0f, 2.5f), (framing.Scale[0], framing.Scale[1], framing.Scale[2]));
        Assert.Equal(3.0f, framing.Tilt);
    }

    [Theory]
    [InlineData("\"Town\"", "Town")]
    [InlineData("Town", "Town")]
    [InlineData("\"Open", "Open")]
    public void Unquoted_Value_DropsTheQuotes(string value, string expected)
    {
        // Act
        string unquoted = BgListText.Unquoted(value);

        // Assert
        Assert.Equal(expected, unquoted);
    }

    [Fact]
    public void Pairs_Block_ListsTopLevelKeysAndSkipsNestedOnes()
    {
        // Arrange
        string block = BgListText.Block(List, "bg001")!;

        // Act
        List<BgPair> pairs = BgListText.Pairs(block);

        // Assert
        Assert.Equal(["Name", "DataFile", "Scale", "ViewRotationX"], pairs.Select(pair => pair.Key));
    }
}
