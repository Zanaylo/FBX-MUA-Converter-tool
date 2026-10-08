using System.Runtime.CompilerServices;

namespace FbxToMua.Core.Geometry;

[InlineArray(Size)]
public struct Float4 : IEquatable<Float4>
{
    public const int Size = 4;

    private float _element;

    public Float4(float x, float y, float z, float w)
    {
        this[0] = x;
        this[1] = y;
        this[2] = z;
        this[3] = w;
    }

    public readonly float Dot(Float4 other)
    {
        return this[0] * other[0] + this[1] * other[1] + this[2] * other[2] + this[3] * other[3];
    }

    public readonly Float4 Negated() => new(-this[0], -this[1], -this[2], -this[3]);

    public readonly bool Equals(Float4 other)
    {
        return this[0] == other[0] && this[1] == other[1] && this[2] == other[2] && this[3] == other[3];
    }

    public override readonly bool Equals(object? obj) => obj is Float4 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(this[0], this[1], this[2], this[3]);

    public override readonly string ToString() => $"{this[0]}, {this[1]}, {this[2]}, {this[3]}";
}
