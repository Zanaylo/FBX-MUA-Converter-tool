using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Export;

public class ReframeUnitTest
{
    private static Float3 Moved(Reframe reframe, Float3 point) => MatrixMath.Transform(point, reframe.StageMatrix());

    private static void AssertNear(Float3 expected, Float3 actual)
    {
        for (int k = 0; k < 3; ++k)
            Assert.True(Tolerance.Near(expected[k], actual[k], 1e-5f), $"{expected} against {actual}");
    }

    [Fact]
    public void StageMatrix_None_IsTheIdentity()
    {
        // Arrange
        Reframe reframe = Reframe.None;

        // Act
        Matrix matrix = reframe.StageMatrix();

        // Assert
        Assert.True(reframe.IsNone);
        Assert.Equal(Matrix.Identity, matrix);
    }

    [Fact]
    public void StageMatrix_CameraRaised_LowersTheStage()
    {
        // Arrange
        Reframe reframe = Reframe.None with { Height = 30.0f };

        // Act
        Float3 moved = Moved(reframe, new Float3(10.0f, 0.0f, 50.0f));

        // Assert
        Assert.False(reframe.IsNone);
        AssertNear(new Float3(10.0f, -30.0f, 50.0f), moved);
    }

    [Fact]
    public void StageMatrix_CameraToTheRight_ShiftsTheStageLeft()
    {
        // Arrange
        Reframe reframe = Reframe.None with { Side = 40.0f };

        // Act
        Float3 moved = Moved(reframe, new Float3());

        // Assert
        AssertNear(new Float3(-40.0f, 0.0f, 0.0f), moved);
    }

    [Fact]
    public void StageMatrix_CameraPulledBack_PushesTheStageAway()
    {
        // Arrange
        Reframe reframe = Reframe.None with { Distance = 80.0f };

        // Act
        Float3 moved = Moved(reframe, new Float3(0.0f, 0.0f, 100.0f));

        // Assert
        AssertNear(new Float3(0.0f, 0.0f, 180.0f), moved);
    }

    [Fact]
    public void StageMatrix_CameraTurnedRight_SwingsWhatWasOnTheRightToTheFront()
    {
        // Arrange
        Reframe reframe = Reframe.None with { Turn = 90.0f };

        // Act
        Float3 moved = Moved(reframe, new Float3(100.0f, 0.0f, 0.0f));

        // Assert
        AssertNear(new Float3(0.0f, 0.0f, 100.0f), moved);
    }

    [Fact]
    public void StageMatrix_Scaled_GrowsTheStageAroundTheFightOrigin()
    {
        // Arrange
        Reframe reframe = Reframe.None with { Scale = 2.0f, Height = 10.0f };

        // Act
        Float3 moved = Moved(reframe, new Float3(5.0f, 5.0f, 5.0f));

        // Assert
        AssertNear(new Float3(10.0f, 0.0f, 10.0f), moved);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(-1.0f)]
    public void StageMatrix_ScaleNotPositive_KeepsTheStageSize(float scale)
    {
        // Arrange
        Reframe reframe = Reframe.None with { Scale = scale };

        // Act
        Float3 moved = Moved(reframe, new Float3(5.0f, 5.0f, 5.0f));

        // Assert
        AssertNear(new Float3(5.0f, 5.0f, 5.0f), moved);
    }

    [Fact]
    public void SceneTilt_AddsToTheStageOwnTiltInWholeDegrees()
    {
        // Arrange
        Reframe reframe = Reframe.None with { Tilt = 2.4f };

        // Act
        int tilt = reframe.SceneTilt(3.3f);

        // Assert
        Assert.Equal(6, tilt);
    }
}
