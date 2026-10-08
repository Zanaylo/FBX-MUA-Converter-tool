using FbxToMua.Core.Binary;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.Mmot;

public sealed class MmotMotion
{
    private const int Sections = 11;
    private const int BoneSection = 1;
    private const int KeyListSection = 2;
    private const int KeySection = 3;
    private const int StringInfoSection = 8;
    private const int StringSection = 9;
    private const int Header = 0x20;
    private const int SectionStride = 0x10;
    private const int KeyStride = 0x20;
    private const int ListStride = 0x40;
    private const int BoneStride = 0xe0;
    private const int UnbindAt = 0x40;
    private const int MostBones = 4096;
    private const float SlerpFlat = 0.001f;

    private static readonly int[] ListCountAt = [0x04, 0x14, 0x24, 0x34];

    private SortedDictionary<int, float[]>[][] _tracks = [];
    private Matrix[] _unbind = [];

    public string Target { get; private set; } = string.Empty;
    public int Bones { get; private set; }
    public int Frames { get; private set; }

    public static MmotMotion Empty(int bones, int frames)
    {
        MmotMotion motion = new()
        {
            Bones = Math.Max(bones, 0),
            Frames = Math.Max(frames, 0),
        };

        motion._tracks = NewTracks(motion.Bones);

        return motion;
    }

    public static MmotMotion? Read(byte[] blob)
    {
        if (blob.Length < Header + Sections * SectionStride || !LittleEndian.Starts(blob, "MMOT"))
            return null;

        uint[] offset = new uint[Sections];
        uint[] count = new uint[Sections];

        for (int i = 0; i < Sections; ++i)
        {
            offset[i] = LittleEndian.U32(blob, Header + i * SectionStride);
            count[i] = LittleEndian.U32(blob, Header + i * SectionStride + 4);
        }

        int bones = (int)count[BoneSection];

        if (bones <= 0 || bones > MostBones)
            return null;

        MmotMotion motion = new() { Bones = bones, _tracks = NewTracks(bones), _unbind = new Matrix[bones] };
        List<string> strings = ReadStrings(blob, offset[StringInfoSection], count[StringInfoSection], offset[StringSection]);
        motion.Target = strings.Count > 1 ? strings[1] : string.Empty;

        for (int bone = 0; bone < bones; ++bone)
        {
            for (int k = 0; k < Matrix.Size; ++k)
                motion._unbind[bone][k] = LittleEndian.F32(blob, offset[BoneSection] + (long)bone * BoneStride + UnbindAt + k * 4);
        }

        return motion.ReadKeys(blob, offset[KeyListSection], offset[KeySection]) ? motion : null;
    }

    public void Add(int bone, MmotKind kind, Float4 value, int frame)
    {
        if (bone < 0 || bone >= _tracks.Length || frame < 0)
            return;

        _tracks[bone][(int)kind][frame] = ((ReadOnlySpan<float>)value)[..Width(kind)].ToArray();
    }

    public bool TryUnbind(int bone, out Matrix unbind)
    {
        unbind = Matrix.Identity;

        if (bone < 0 || bone >= _unbind.Length || _unbind[bone][15] == 0.0f)
            return false;

        unbind = _unbind[bone];
        return true;
    }

    public Matrix Pose(int bone, int frame, Float3 restTranslate, Float3 restRotate, Float3 restScale)
    {
        SortedDictionary<int, float[]>[] tracks = bone >= 0 && bone < _tracks.Length ? _tracks[bone] : NewTracks(1)[0];

        Float3 translate = Value(tracks[(int)MmotKind.Translation], frame, restTranslate);
        Float3 scale = Value(tracks[(int)MmotKind.Scale], frame, restScale);
        Matrix turn = tracks[(int)MmotKind.Turn].Count == 0
            ? Euler(Value(tracks[(int)MmotKind.Rotation], frame, restRotate))
            : Quaternion(Turned(tracks[(int)MmotKind.Turn], frame));

        Matrix posed = Matrix.Identity;

        for (int r = 0; r < 3; ++r)
        {
            for (int c = 0; c < 3; ++c)
                posed[r * 4 + c] = scale[r] * turn[r * 4 + c];
        }

        posed[12] = translate[0];
        posed[13] = translate[1];
        posed[14] = translate[2];

        return posed;
    }

    private static SortedDictionary<int, float[]>[][] NewTracks(int bones)
    {
        SortedDictionary<int, float[]>[][] tracks = new SortedDictionary<int, float[]>[bones][];

        for (int bone = 0; bone < bones; ++bone)
            tracks[bone] = [new(), new(), new(), new()];

        return tracks;
    }

    private static int Width(MmotKind kind) => kind == MmotKind.Turn ? 4 : 3;

    private static List<string> ReadStrings(byte[] blob, uint infoAt, uint count, uint baseAt)
    {
        List<string> strings = [];

        for (uint i = 0; i < count; ++i)
        {
            long at = infoAt + i * 0x10L;
            strings.Add(LittleEndian.Ascii(blob, baseAt + LittleEndian.U32(blob, at), (int)LittleEndian.U32(blob, at + 4)));
        }

        return strings;
    }

    private bool ReadKeys(byte[] blob, uint list, uint keysAt)
    {
        long at = keysAt;

        for (int bone = 0; bone < Bones; ++bone)
        {
            for (int which = 0; which < MmotBone.Tracks; ++which)
            {
                uint keys = LittleEndian.U32(blob, list + (long)bone * ListStride + ListCountAt[which]);
                int width = Width((MmotKind)which);

                for (uint i = 0; i < keys; ++i)
                {
                    if (at + KeyStride > blob.Length)
                        return false;

                    int frame = LittleEndian.I32(blob, at + 0x10);
                    float[] value = new float[width];

                    for (int k = 0; k < width; ++k)
                        value[k] = LittleEndian.F32(blob, at + k * 4);

                    _tracks[bone][which][frame] = value;
                    Frames = Math.Max(Frames, frame + 1);
                    at += KeyStride;
                }
            }
        }

        return true;
    }

    private static bool Bracket(SortedDictionary<int, float[]> track, int frame, out float[] below, out int belowFrame,
        out float[]? above, out int aboveFrame)
    {
        below = [];
        belowFrame = 0;
        above = null;
        aboveFrame = 0;
        bool hasBelow = false;

        foreach (KeyValuePair<int, float[]> key in track)
        {
            if (key.Key >= frame)
            {
                above = key.Value;
                aboveFrame = key.Key;
                break;
            }

            below = key.Value;
            belowFrame = key.Key;
            hasBelow = true;
        }

        return hasBelow;
    }

    private static Float3 Value(SortedDictionary<int, float[]> track, int frame, Float3 fallback)
    {
        if (track.Count == 0)
            return fallback;

        bool hasBelow = Bracket(track, frame, out float[] below, out int belowFrame, out float[]? above, out int aboveFrame);

        if (above is not null && (aboveFrame == frame || !hasBelow))
            return Copy3(above);

        if (above is null)
            return Copy3(below);

        float span = aboveFrame - belowFrame;
        float t = span == 0.0f ? 0.0f : (frame - belowFrame) / span;
        Float3 value = new();

        for (int i = 0; i < 3; ++i)
        {
            float low = i < below.Length ? below[i] : 0.0f;
            float high = i < above.Length ? above[i] : 0.0f;
            value[i] = low + (high - low) * t;
        }

        return value;
    }

    private static Float4 Turned(SortedDictionary<int, float[]> track, int frame)
    {
        bool hasBelow = Bracket(track, frame, out float[] below, out int belowFrame, out float[]? above, out int aboveFrame);

        if (above is not null && (aboveFrame == frame || !hasBelow))
            return Copy4(above);

        if (above is null || below.Length < 4 || above.Length < 4)
            return Copy4(below);

        float span = aboveFrame - belowFrame;
        float t = span == 0.0f ? 0.0f : (frame - belowFrame) / span;

        return Slerp(below, above, t);
    }

    private static Float4 Slerp(float[] from, float[] to, float t)
    {
        float low = 1.0f - t;
        float high = t;
        float dot = 0.0f;

        for (int k = 0; k < 4; ++k)
            dot += from[k] * to[k];

        if (dot < 0.0f)
        {
            high = -high;
            dot = -dot;
        }

        if (1.0f - dot > SlerpFlat)
        {
            float theta = MathF.Acos(dot);
            low = MathF.Sin(theta * low) / MathF.Sin(theta);
            high = MathF.Sin(theta * high) / MathF.Sin(theta);
        }

        Float4 turned = new();

        for (int k = 0; k < 4; ++k)
            turned[k] = low * from[k] + high * to[k];

        return turned;
    }

    private static Float3 Copy3(float[] value)
    {
        Float3 copy = new();

        for (int i = 0; i < 3; ++i)
            copy[i] = i < value.Length ? value[i] : 0.0f;

        return copy;
    }

    private static Float4 Copy4(float[] value)
    {
        Float4 copy = new();

        for (int i = 0; i < 4; ++i)
            copy[i] = i < value.Length ? value[i] : 0.0f;

        return copy;
    }

    private static Matrix Rotation(int axis, float angle)
    {
        Matrix rotation = Matrix.Identity;
        float cosine = MathF.Cos(angle);
        float sine = MathF.Sin(angle);
        (int first, int second) = axis switch { 0 => (1, 2), 1 => (2, 0), _ => (0, 1) };

        rotation[first * 4 + first] = cosine;
        rotation[first * 4 + second] = sine;
        rotation[second * 4 + first] = -sine;
        rotation[second * 4 + second] = cosine;

        return rotation;
    }

    private static Matrix Euler(Float3 rotate)
    {
        Matrix turned = Matrix.Identity;

        foreach (int axis in new[] { 1, 0, 2 })
            turned = MatrixMath.Multiply(turned, Rotation(axis, rotate[axis]));

        return turned;
    }

    private static Matrix Quaternion(Float4 q)
    {
        Pose pose = new() { Turn = q, Scale = Float3.All(1.0f) };

        return PoseMath.Compose(pose);
    }
}
