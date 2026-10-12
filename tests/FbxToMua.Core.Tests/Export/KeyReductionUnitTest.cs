using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Export;

public class KeyReductionUnitTest
{
    private const int Frames = 40;

    private static List<Pose> Thrown()
    {
        List<Pose> poses = [];

        for (int frame = 0; frame <= Frames; ++frame)
        {
            Pose pose = Pose.Rest;
            pose.Translation = new Float3(frame, frame * (Frames - frame) * 0.5f, 0.0f);
            poses.Add(pose);
        }

        return poses;
    }

    private static List<Pose> Spun()
    {
        List<Pose> poses = [];

        for (int frame = 0; frame <= Frames; ++frame)
        {
            Pose pose = Pose.Rest;
            pose.Rotation = new Float3(0.0f, frame * frame * 0.002f, 0.0f);
            poses.Add(PoseMath.Turned(pose));
        }

        return poses;
    }

    private static List<Pose> Pulsing()
    {
        List<Pose> poses = [];

        for (int frame = 0; frame <= Frames; ++frame)
        {
            Pose pose = Pose.Rest;
            pose.Scale = Float3.All(1.0f + MathF.Sin(frame * 0.3f) * 0.5f);
            poses.Add(pose);
        }

        return poses;
    }

    [Fact]
    public void Kept_MovingWithoutTurning_KeepsOnlyTheEndsOfTheTurn()
    {
        // Arrange
        List<Pose> poses = Thrown();

        // Act
        List<int> turn = KeyReduction.Kept(poses, PoseChannel.Turn);
        List<int> size = KeyReduction.Kept(poses, PoseChannel.Size);
        List<int> move = KeyReduction.Kept(poses, PoseChannel.Move);

        // Assert
        Assert.Equal([0, Frames], turn);
        Assert.Equal([0, Frames], size);
        Assert.True(move.Count > 2);
    }

    [Fact]
    public void Kept_TurningInPlace_KeepsOnlyTheEndsOfTheMove()
    {
        // Arrange
        List<Pose> poses = Spun();

        // Act
        List<int> move = KeyReduction.Kept(poses, PoseChannel.Move);
        List<int> turn = KeyReduction.Kept(poses, PoseChannel.Turn);

        // Assert
        Assert.Equal([0, Frames], move);
        Assert.True(turn.Count > 2);
    }

    [Fact]
    public void Kept_Pulsing_KeepsTheSizeKeysOnly()
    {
        // Arrange
        List<Pose> poses = Pulsing();

        // Act
        List<int> size = KeyReduction.Kept(poses, PoseChannel.Size);
        List<int> turn = KeyReduction.Kept(poses, PoseChannel.Turn);

        // Assert
        Assert.True(size.Count > 2);
        Assert.Equal([0, Frames], turn);
    }

    [Fact]
    public void Kept_LongStill_StillKeysEveryLongestSpan()
    {
        // Arrange
        List<Pose> poses = Enumerable.Repeat(Pose.Rest, 600).ToList();

        // Act
        List<int> move = KeyReduction.Kept(poses, PoseChannel.Move);

        // Assert
        Assert.Equal([0, 256, 512, 599], move);
    }

    [Fact]
    public void Kept_Thrown_RebuildsEveryFrameWithinTheTolerance()
    {
        // Arrange
        List<Pose> poses = Thrown();

        // Act
        List<int> move = KeyReduction.Kept(poses, PoseChannel.Move);

        // Assert
        for (int k = 0; k + 1 < move.Count; ++k)
        {
            Pose from = poses[move[k]];
            Pose to = poses[move[k + 1]];

            for (int frame = move[k]; frame <= move[k + 1]; ++frame)
            {
                float t = (float)(frame - move[k]) / (move[k + 1] - move[k]);

                for (int c = 0; c < 3; ++c)
                {
                    float rebuilt = from.Translation[c] + (to.Translation[c] - from.Translation[c]) * t;
                    Assert.True(Tolerance.Near(poses[frame].Translation[c], rebuilt, 1e-3f));
                }
            }
        }
    }
}
