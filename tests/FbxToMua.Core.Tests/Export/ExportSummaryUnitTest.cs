using FbxToMua.Core.Export;

namespace FbxToMua.Core.Tests.Export;

public class ExportSummaryUnitTest
{
    [Fact]
    public void Of_FloorTakesTheShadow_SaysSo()
    {
        // Arrange
        ExportResult result = new() { Stage = "bg_test", ShadowFloors = 2 };

        // Act
        string summary = ExportSummary.Of(result);

        // Assert
        Assert.Contains("2 floor mesh(es) take the fighters' shadow.", summary);
        Assert.DoesNotContain("cut", summary);
    }

    [Fact]
    public void Of_FloorCutForALeaningShadow_SaysSo()
    {
        // Arrange
        ExportResult result = new() { Stage = "bg_test", ShadowFloors = 1, ShadowCuts = 1 };

        // Act
        string summary = ExportSummary.Of(result);

        // Assert
        Assert.Contains("1 of them cut to the strip BBTAG casts this leaning shadow on.", summary);
    }

    [Fact]
    public void Of_NoFloorTakesTheShadow_WarnsThatTheGameDrawsNone()
    {
        // Arrange
        ExportResult result = new() { Stage = "bg_test" };

        // Act
        string summary = ExportSummary.Of(result);

        // Assert
        Assert.Contains("No floor takes the fighters' shadow, so the game draws none.", summary);
    }
}
