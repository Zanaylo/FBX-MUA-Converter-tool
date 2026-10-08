using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests;

public static class Tolerance
{
    public static bool Near(float left, float right, float tolerance)
    {
        return MathF.Abs(left - right) <= tolerance * (1.0f + MathF.Abs(left) + MathF.Abs(right));
    }

    public static bool Near(Matrix left, Matrix right, float tolerance)
    {
        for (int i = 0; i < Matrix.Size; ++i)
        {
            if (!Near(left[i], right[i], tolerance))
                return false;
        }

        return true;
    }
}
