using FbxToMua.Core.Export;

namespace FbxToMua.Core.Tests.Export;

public class SceneLightUnitTest
{
    [Fact]
    public void Vector_Default_IsTheShippedLight()
    {
        // Arrange
        Reframe reframe = Reframe.None;

        // Act
        int[] vector = SceneLight.Vector(reframe);

        // Assert
        Assert.Equal([0, 900, -1000], vector);
    }

    [Fact]
    public void Vector_QuarterTurn_PointsAlongNegativeX()
    {
        // Arrange
        Reframe reframe = Reframe.None with { ShadowTurn = 90.0f };

        // Act
        int[] vector = SceneLight.Vector(reframe);

        // Assert
        Assert.Equal([-1000, 900, 0], vector);
    }

    [Fact]
    public void Vector_LongerShadow_LowersTheLight()
    {
        // Arrange
        Reframe reframe = Reframe.None with { ShadowLength = 2.0f };

        // Act
        int[] vector = SceneLight.Vector(reframe);

        // Assert
        Assert.Equal([0, 500, -1000], vector);
    }

    [Fact]
    public void Vector_NoLength_KeepsTheLightAboveTheGround()
    {
        // Arrange
        Reframe reframe = Reframe.None with { ShadowLength = 0.0f };

        // Act
        int[] vector = SceneLight.Vector(reframe);

        // Assert
        Assert.True(vector[1] > 0 && vector[1] <= 10000);
    }

    [Fact]
    public void Strength_Default_IsTheShippedStrength()
    {
        // Arrange
        Reframe reframe = Reframe.None;

        // Act
        int strength = SceneLight.Strength(reframe);

        // Assert
        Assert.Equal(750, strength);
    }

    [Fact]
    public void Reach_Default_FallsTowardsTheCamera()
    {
        // Arrange
        Reframe reframe = Reframe.None;

        // Act
        (float x, float z) = SceneLight.Reach(reframe, 180.0f);

        // Assert
        Assert.Equal(0.0f, x, 1e-3f);
        Assert.Equal(-200.0f, z, 1e-3f);
    }

    [Fact]
    public void Reach_QuarterTurn_FallsAlongTheVector()
    {
        // Arrange
        Reframe reframe = Reframe.None with { ShadowTurn = 90.0f };

        // Act
        (float x, float z) = SceneLight.Reach(reframe, 180.0f);

        // Assert
        Assert.Equal(-200.0f, x, 1e-3f);
        Assert.Equal(0.0f, z, 1e-3f);
    }

    [Fact]
    public void Reach_TurnedRound_CastsTheShadowStraightBack()
    {
        // Arrange
        Reframe reframe = Reframe.None with { ShadowTurn = 180.0f };

        // Act
        (float x, float z) = SceneLight.Reach(reframe, 180.0f);

        // Assert
        Assert.Equal(0.0f, x, 1e-3f);
        Assert.True(z > 150.0f);
    }

    [Fact]
    public void Reach_NoLength_FallsAlmostStraightDown()
    {
        // Arrange
        Reframe reframe = Reframe.None with { ShadowLength = 0.0f };

        // Act
        (float x, float z) = SceneLight.Reach(reframe, 180.0f);

        // Assert
        Assert.True(Math.Abs(x) < 5.0f && Math.Abs(z) < 5.0f);
    }
}
