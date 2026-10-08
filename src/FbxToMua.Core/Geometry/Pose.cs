namespace FbxToMua.Core.Geometry;

public struct Pose
{
    public Float3 Translation;
    public Float3 Rotation;
    public Float4 Turn;
    public Float3 Scale;

    public static Pose Rest
    {
        get
        {
            Pose rest = new() { Scale = Float3.All(1.0f) };

            return PoseMath.Turned(rest);
        }
    }

    public readonly bool SameAs(Pose other)
    {
        return SameBits(Translation, other.Translation) && SameBits(Turn, other.Turn) && SameBits(Scale, other.Scale);
    }

    private static bool SameBits(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        return System.Runtime.InteropServices.MemoryMarshal.AsBytes(left).SequenceEqual(System.Runtime.InteropServices.MemoryMarshal.AsBytes(right));
    }
}
