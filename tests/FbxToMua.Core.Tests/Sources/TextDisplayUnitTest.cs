using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Tests.Sources;

public class TextDisplayUnitTest
{
    [Fact]
    public void Stored_NameWithAStar_ReadsBackThroughOf()
    {
        // Arrange
        string name = "Hi☆sCoool! SeHa Girls Stage";

        // Act
        string stored = TextDisplay.Stored(name);

        // Assert
        Assert.Equal(name, TextDisplay.Of(stored));
    }

    [Fact]
    public void Stored_AsciiName_StaysTheSame()
    {
        // Arrange
        string name = "Quiet Park";

        // Act
        string stored = TextDisplay.Stored(name);

        // Assert
        Assert.Equal(name, stored);
    }
}
