using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Mmot;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Export;

public class IntroCameraUnitTest
{
    private static MmotMotion Motion() => MmotMotion.Read(IntroCamera.Still("bg_small"))!;

    [Fact]
    public void Still_Stage_DrivesColosseumsCameraBoneOverTheWholeIntro()
    {
        // Arrange
        string stage = "bg_small";

        // Act
        MmotMotion motion = MmotMotion.Read(IntroCamera.Still(stage))!;

        // Assert
        Assert.Equal(1, motion.Bones);
        Assert.Equal("Bone_camera001", motion.Target);
        Assert.Equal(IntroCamera.Frames + 1, motion.Frames);
    }

    [Fact]
    public void FileName_Stage_IsNamedLikeBbcfsCameras()
    {
        // Arrange
        string stage = "bg_small";

        // Act
        string name = IntroCamera.FileName(stage);

        // Assert
        Assert.Equal("small_cam_000.mmot", name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(IntroCamera.Frames / 2)]
    [InlineData(IntroCamera.Frames)]
    public void Pose_AnyFrame_SitsAtTheBattleEyeTurnedLikeColosseum(int frame)
    {
        // Arrange
        MmotMotion motion = Motion();

        // Act
        Matrix posed = motion.Pose(0, frame, new Float3(), new Float3(), Float3.All(1.0f));

        // Assert
        Assert.True(Tolerance.Near(posed[12], 0.0f, 1e-5f));
        Assert.True(Tolerance.Near(posed[13], 100.0f, 1e-5f));
        Assert.True(Tolerance.Near(posed[14], -320.0f, 1e-5f));
        Assert.True(Tolerance.Near(posed[1], -1.0f, 1e-5f));
        Assert.True(Tolerance.Near(posed[4], 1.0f, 1e-5f));
    }

    [Fact]
    public void Unbind_Camera_UndoesTheBindRotation()
    {
        // Arrange
        MmotMotion motion = Motion();
        Matrix local = motion.Pose(0, 0, new Float3(), new Float3(), Float3.All(1.0f));
        local[12] = local[13] = local[14] = 0.0f;

        // Act
        bool stored = motion.TryUnbind(0, out Matrix unbind);

        // Assert
        Assert.True(stored);
        Assert.True(Tolerance.Near(Matrix.Identity, MatrixMath.Multiply(local, unbind), 1e-5f));
    }
}
