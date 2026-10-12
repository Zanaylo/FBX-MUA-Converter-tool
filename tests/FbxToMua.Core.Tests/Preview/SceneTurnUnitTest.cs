using FbxToMua.Core.Geometry;
using FbxToMua.Core.Preview;

namespace FbxToMua.Core.Tests.Preview;

public class SceneTurnUnitTest
{
    private const float Close = 1e-5f;

    [Fact]
    public void Of_NoTilt_LeavesTheStageAlone()
    {
        // Arrange
        const int level = 0;

        // Act
        Matrix turn = SceneTurn.Of(level);

        // Assert
        Assert.Equal(Matrix.Identity, turn);
    }

    [Fact]
    public void Of_PositiveTilt_DropsTheFarStageTheWayRotationXDoes()
    {
        // Arrange
        Float3 far = new(0.0f, 0.0f, 1000.0f);

        // Act
        Float3 turned = MatrixMath.Transform(far, SceneTurn.Of(90));

        // Assert
        Assert.Equal(0.0f, turned[0], Close);
        Assert.Equal(-1000.0f, turned[1], 1e-2f);
        Assert.Equal(0.0f, turned[2], 1e-2f);
    }

    [Fact]
    public void Of_AnyTilt_TurnsAboutTheWorldOrigin()
    {
        // Arrange
        Float3 origin = new();

        // Act
        Float3 turned = MatrixMath.Transform(origin, SceneTurn.Of(10));

        // Assert
        Assert.Equal(origin, turned);
    }
}
