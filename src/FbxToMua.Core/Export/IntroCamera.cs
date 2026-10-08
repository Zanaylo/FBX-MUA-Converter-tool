using FbxToMua.Core.Formats.Mmot;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public static class IntroCamera
{
    public const int Frames = 600;
    private const string StagePrefix = "bg_";
    private const string FileSuffix = "_cam_000.mmot";
    private const string TakeSuffix = "_cam.DIG";
    private const string CameraBone = "Bone_camera001";
    private const float QuarterTurn = (float)(Math.PI / 2.0);

    public static string FileName(string stage) => Bare(stage) + FileSuffix;

    public static byte[] Still(string stage)
    {
        return MmotWriter.Build(new MmotTake(Bare(stage) + TakeSuffix, CameraBone, [], Frames, [Bone()]));
    }

    private static string Bare(string stage) => stage.StartsWith(StagePrefix, StringComparison.Ordinal) ? stage[StagePrefix.Length..] : stage;

    private static Pose BattlePose()
    {
        Pose pose = new()
        {
            Translation = new Float3(0.0f, (float)BattleCamera.EyeHeight, (float)-BattleCamera.EyeDistance),
            Rotation = new Float3(0.0f, 0.0f, -QuarterTurn),
            Scale = Float3.All(1.0f),
        };

        return PoseMath.Turned(pose);
    }

    private static MmotBone Bone()
    {
        Pose pose = BattlePose();
        Pose bind = pose;
        bind.Translation = new Float3();

        MmotBone bone = MmotBone.Still();
        bone.Local = PoseMath.Compose(bind);
        bone.Unbind = MatrixMath.Invert(bone.Local);

        foreach (int frame in new[] { 0, Frames })
            bone.Hold(pose, frame);

        return bone;
    }
}
