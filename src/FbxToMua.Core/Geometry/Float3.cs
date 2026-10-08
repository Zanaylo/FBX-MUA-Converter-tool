using System.Runtime.CompilerServices;

namespace FbxToMua.Core.Geometry;

[InlineArray(Size)]
public struct Float3 : IEquatable<Float3>
{
    public const int Size = 3;

    private float _element;

    public Float3(float x, float y, float z)
    {
        this[0] = x;
        this[1] = y;
        this[2] = z;
    }

    public static Float3 All(float value) => new(value, value, value);

    public readonly bool Equals(Float3 other) => this[0] == other[0] && this[1] == other[1] && this[2] == other[2];

    public override readonly bool Equals(object? obj) => obj is Float3 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(this[0], this[1], this[2]);

    public override readonly string ToString() => $"{this[0]}, {this[1]}, {this[2]}";
}
