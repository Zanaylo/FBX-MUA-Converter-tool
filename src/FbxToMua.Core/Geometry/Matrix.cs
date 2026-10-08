using System.Runtime.CompilerServices;

namespace FbxToMua.Core.Geometry;

[InlineArray(Size)]
public struct Matrix : IEquatable<Matrix>
{
    public const int Size = 16;

    private float _element;

    public static Matrix Identity
    {
        get
        {
            Matrix identity = new();

            for (int i = 0; i < Size; i += 5)
                identity[i] = 1.0f;

            return identity;
        }
    }

    public static Matrix From(ReadOnlySpan<float> values)
    {
        Matrix matrix = new();
        values[..Size].CopyTo(matrix);

        return matrix;
    }

    public readonly bool Equals(Matrix other)
    {
        for (int i = 0; i < Size; ++i)
        {
            if (this[i] != other[i])
                return false;
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is Matrix other && Equals(other);

    public override readonly int GetHashCode()
    {
        HashCode hash = new();

        for (int i = 0; i < Size; ++i)
            hash.Add(this[i]);

        return hash.ToHashCode();
    }

    public override readonly string ToString() => string.Join(", ", ((ReadOnlySpan<float>)this).ToArray());
}
