using System.Runtime.CompilerServices;

namespace FbxToMua.Core.Geometry;

[InlineArray(Size)]
public struct Float3 : IEquatable<Float3>
{
    public const int Size = 3;
    private const float Shortest = 1e-12f;

    private float _element;

    public Float3(float x, float y, float z)
    {
        this[0] = x;
        this[1] = y;
        this[2] = z;
    }

    public static Float3 All(float value) => new(value, value, value);

    public static Float3 Lerp(Float3 from, Float3 to, float share)
    {
        return new Float3(from[0] + (to[0] - from[0]) * share, from[1] + (to[1] - from[1]) * share, from[2] + (to[2] - from[2]) * share);
    }

    public static bool TryNormalise(ref Float3 vector)
    {
        float length = MathF.Sqrt(vector[0] * vector[0] + vector[1] * vector[1] + vector[2] * vector[2]);

        if (length < Shortest)
            return false;

        for (int k = 0; k < Size; ++k)
            vector[k] /= length;

        return true;
    }

    public readonly bool Equals(Float3 other) => this[0] == other[0] && this[1] == other[1] && this[2] == other[2];

    public override readonly bool Equals(object? obj) => obj is Float3 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(this[0], this[1], this[2]);

    public override readonly string ToString() => $"{this[0]}, {this[1]}, {this[2]}";
}
