using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.Mmot;

public enum MmotKind
{
    Translation = 0,
    Rotation = 1,
    Turn = 2,
    Scale = 3,
}

public readonly record struct MmotKey(Float4 Value, int Frame);

public sealed class MmotBone
{
    public const int Tracks = 4;

    public Matrix Local;
    public Matrix Unbind;
    public Matrix ParentUnbind;

    public List<MmotKey>[] Track { get; } = [[], [], [], []];

    public static MmotBone Still()
    {
        return new MmotBone { Local = Matrix.Identity, Unbind = Matrix.Identity, ParentUnbind = Matrix.Identity };
    }

    public void Hold(Pose pose, int frame)
    {
        Track[(int)MmotKind.Translation].Add(new MmotKey(Widened(pose.Translation), frame));
        Track[(int)MmotKind.Rotation].Add(new MmotKey(Widened(pose.Rotation), frame));
        Track[(int)MmotKind.Turn].Add(new MmotKey(pose.Turn, frame));
        Track[(int)MmotKind.Scale].Add(new MmotKey(Widened(pose.Scale), frame));
    }

    private static Float4 Widened(Float3 value) => new(value[0], value[1], value[2], 0.0f);
}

public sealed record MmotTake(string Name, string Root, IReadOnlyList<string> Meshes, int Frames, IReadOnlyList<MmotBone> Bones);
