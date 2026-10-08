using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Mmot;
using FbxToMua.Core.Geometry;
using FbxToMua.Core.Tests.Geometry;

namespace FbxToMua.Core.Tests.Formats;

public class MmotUnitTest
{
    private static MmotTake TwoBoneTake()
    {
        MmotBone still = MmotBone.Still();
        still.Hold(Pose.Rest, 0);
        still.Hold(Pose.Rest, 10);

        Pose there = Pose.Rest;
        there.Translation = new Float3(4.0f, 0.0f, 0.0f);

        MmotBone moving = MmotBone.Still();
        moving.Hold(Pose.Rest, 0);
        moving.Hold(PoseMath.Turned(there), 10);

        return new MmotTake("mesh_001.DIG", "mesh_001", ["mesh_001"], 10, [still, moving]);
    }

    [Fact]
    public void Build_Take_StartsWithMagicAndVersion()
    {
        // Arrange
        MmotTake take = TwoBoneTake();

        // Act
        byte[] blob = MmotWriter.Build(take);

        // Assert
        Assert.True(LittleEndian.Starts(blob, "MMOT"));
        Assert.Equal(0x3ebu, LittleEndian.U32(blob, 4));
    }

    [Fact]
    public void Read_WrittenTake_GivesBonesFramesAndTargetBack()
    {
        // Arrange
        byte[] blob = MmotWriter.Build(TwoBoneTake());

        // Act
        MmotMotion motion = MmotMotion.Read(blob)!;

        // Assert
        Assert.Equal(2, motion.Bones);
        Assert.Equal(11, motion.Frames);
        Assert.Equal("mesh_001", motion.Target);
        Assert.True(motion.TryUnbind(1, out _));
    }

    [Fact]
    public void Pose_HalfwayFrame_InterpolatesTheTranslation()
    {
        // Arrange
        MmotMotion motion = MmotMotion.Read(MmotWriter.Build(TwoBoneTake()))!;
        Pose rest = Pose.Rest;

        // Act
        Matrix posed = motion.Pose(1, 5, rest.Translation, rest.Rotation, rest.Scale);

        // Assert
        Assert.True(Tolerance.Near(posed[12], 2.0f, 1e-5f));
    }

    [Fact]
    public void Pose_QuaternionKey_BuildsWhatPoseMathComposes()
    {
        // Arrange
        Pose pose = PoseMathUnitTest.TurnedPose();
        Matrix matrix = PoseMath.Compose(pose);
        Pose split = PoseMath.Split(matrix);
        MmotMotion motion = MmotMotion.Empty(1, 2);
        motion.Add(0, MmotKind.Translation, new Float4(pose.Translation[0], pose.Translation[1], pose.Translation[2], 0.0f), 0);
        motion.Add(0, MmotKind.Turn, split.Turn, 0);
        motion.Add(0, MmotKind.Scale, new Float4(pose.Scale[0], pose.Scale[1], pose.Scale[2], 0.0f), 0);

        // Act
        Matrix sampled = motion.Pose(0, 0, pose.Translation, pose.Rotation, pose.Scale);

        // Assert
        Assert.True(Tolerance.Near(matrix, sampled, 1e-5f));
    }

    [Fact]
    public void Read_NotAMotion_GivesNothing()
    {
        // Arrange
        byte[] garbage = new byte[300];

        // Act
        MmotMotion? motion = MmotMotion.Read(garbage);

        // Assert
        Assert.Null(motion);
    }
}
