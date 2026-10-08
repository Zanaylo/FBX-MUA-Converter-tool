using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Geometry;

public class PoseMathUnitTest
{
    public static Pose TurnedPose()
    {
        Pose pose = new();
        pose.Translation = new Float3(12.0f, -3.0f, 40.0f);
        pose.Scale = new Float3(2.0f, 0.5f, 1.5f);
        pose.Rotation = new Float3(0.3f, -1.1f, 0.7f);

        return PoseMath.Turned(pose);
    }

    [Fact]
    public void Split_ComposedTrs_GivesTheMatrixBack()
    {
        // Arrange
        Matrix matrix = PoseMath.Compose(TurnedPose());

        // Act
        bool split = PoseMath.TrySplit(matrix, out Pose read);
        Matrix again = PoseMath.Compose(read);

        // Assert
        Assert.True(split);
        Assert.True(Tolerance.Near(matrix, again, 1e-5f));
    }

    [Fact]
    public void Split_ComposedTrs_GivesEulerAnglesAndScaleBack()
    {
        // Arrange
        Pose pose = TurnedPose();

        // Act
        PoseMath.TrySplit(PoseMath.Compose(pose), out Pose read);

        // Assert
        for (int k = 0; k < 3; ++k)
        {
            Assert.True(Tolerance.Near(read.Rotation[k], pose.Rotation[k], 1e-4f));
            Assert.True(Tolerance.Near(read.Scale[k], pose.Scale[k], 1e-5f));
        }
    }

    [Fact]
    public void Split_MirroredMatrix_ComposesBack()
    {
        // Arrange
        Matrix mirrored = PoseMath.Compose(TurnedPose());

        for (int k = 0; k < 3; ++k)
            mirrored[k] = -mirrored[k];

        // Act
        bool split = PoseMath.TrySplit(mirrored, out Pose read);

        // Assert
        Assert.True(split);
        Assert.True(Tolerance.Near(mirrored, PoseMath.Compose(read), 1e-5f));
    }

    [Fact]
    public void Split_FlatRow_Fails()
    {
        // Arrange
        Matrix flat = Matrix.Identity;
        flat[0] = 0.0f;

        // Act
        bool split = PoseMath.TrySplit(flat, out _);

        // Assert
        Assert.False(split);
    }
}
