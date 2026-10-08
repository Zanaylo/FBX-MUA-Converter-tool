using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Export;

public class FarFieldUnitTest
{
    private static readonly Float3 Ahead = new(5.0f, 40.0f, 1000.0f);

    public static bool SameScreen(Float3 left, Float3 right, float tolerance)
    {
        float eyeY = (float)BattleCamera.EyeHeight;
        float eyeZ = -(float)BattleCamera.EyeDistance;
        float leftDepth = left[2] - eyeZ;
        float rightDepth = right[2] - eyeZ;

        return Tolerance.Near(left[0] / leftDepth, right[0] / rightDepth, tolerance)
            && Tolerance.Near((left[1] - eyeY) / leftDepth, (right[1] - eyeY) / rightDepth, tolerance);
    }

    private static DepthSpan Sky()
    {
        DepthSpan sky = DepthSpan.Empty;
        sky.Widen(new Float3(0.0f, 0.0f, 117680.0f));
        sky.Widen(new Float3(0.0f, 0.0f, 121680.0f));

        return sky;
    }

    [Fact]
    public void Depth_PointAhead_IsMeasuredFromTheEye()
    {
        // Arrange
        Float3 point = Ahead;

        // Act
        double depth = FarField.Depth(point);

        // Assert
        Assert.True(Tolerance.Near((float)depth, 1320.0f, 1e-6f));
    }

    [Fact]
    public void Widen_TwoPoints_HoldsThemBoth()
    {
        // Arrange
        DepthSpan sky = DepthSpan.Empty;
        Float3 near = new(0.0f, 0.0f, 117680.0f);
        Float3 far = new(0.0f, 0.0f, 121680.0f);

        // Act
        sky.Widen(near);
        sky.Widen(far);

        // Assert
        Assert.True(Tolerance.Near((float)sky.Nearest, 118000.0f, 1e-6f));
        Assert.True(Tolerance.Near((float)sky.Farthest, 122000.0f, 1e-6f));
    }

    [Fact]
    public void Widen_PointBehindTheEye_DoesNotCount()
    {
        // Arrange
        DepthSpan behind = DepthSpan.Empty;
        Float3 behindTheEye = new(0.0f, 0.0f, -500.0f);

        // Act
        behind.Widen(behindTheEye);
        behind.Widen(Ahead);

        // Assert
        Assert.True(Tolerance.Near((float)behind.Nearest, 1320.0f, 1e-6f));
    }

    [Fact]
    public void Decide_StageSkyAndStars_PullsOnlyWhatIsPastTheFarPlane()
    {
        // Arrange
        DepthSpan stage = new() { Nearest = 300.0, Farthest = 50000.0 };
        DepthSpan stars = new() { Nearest = 64000.0, Farthest = 240000.0 };

        // Act
        List<FarPull> pulls = FarField.Decide([stage, Sky(), stars]);

        // Assert
        Assert.False(pulls[0].Pulled);
        Assert.Equal(1.0f, pulls[0].Factor);
        Assert.True(pulls[1].Pulled);
        Assert.True(Tolerance.Near(pulls[1].Factor, (float)(FarField.Reach / 122000.0), 1e-6f));
        Assert.True(pulls[1].KeepsDepth);
        Assert.True(pulls[2].Pulled);
        Assert.False(pulls[2].KeepsDepth);
    }

    [Fact]
    public void Decide_FloorFromTheFighters_IsNeverPulled()
    {
        // Arrange
        DepthSpan floor = new() { Nearest = 10.0, Farthest = 150000.0 };

        // Act
        List<FarPull> pulls = FarField.Decide([floor, Sky()]);

        // Assert
        Assert.False(pulls[0].Pulled);
        Assert.True(pulls[1].Pulled);
        Assert.False(pulls[1].KeepsDepth);
    }

    [Fact]
    public void Pull_Half_KeepsThePointOnItsRayAndHalvesTheDepth()
    {
        // Arrange
        Float3 point = new(3000.0f, -9000.0f, 180000.0f);

        // Act
        Float3 moved = MatrixMath.Transform(point, FarField.Pull(0.5f));

        // Assert
        Assert.True(SameScreen(point, moved, 1e-6f));
        Assert.True(Tolerance.Near((float)FarField.Depth(moved), (float)FarField.Depth(point) * 0.5f, 1e-6f));
    }
}
