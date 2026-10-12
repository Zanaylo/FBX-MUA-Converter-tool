using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Export;

public class StageDepthUnitTest
{
    [Fact]
    public void Of_StageOrigin_IsTenUnitsFromTheEye()
    {
        // Arrange
        Framing framing = Framing.Neutral;

        // Act
        float depth = StageDepth.Of(framing, 0.0f);

        // Assert
        Assert.Equal(10.0f, depth);
    }

    [Fact]
    public void Of_ScaledAndPlacedStage_MovesWithBoth()
    {
        // Arrange
        Framing framing = Framing.Neutral with { Scale = Float3.All(2.0f), Position = new Float3(0.0f, 0.0f, 5.0f) };

        // Act
        float depth = StageDepth.Of(framing, -10.0f);

        // Assert
        Assert.Equal(25.0f, depth);
    }
}
