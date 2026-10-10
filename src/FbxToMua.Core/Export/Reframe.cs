using System.Text.Json.Serialization;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public readonly record struct Reframe(float Side, float Height, float Distance, float Turn, float Scale, float Tilt)
{
    private const float Unscaled = 1.0f;
    private const double DegreesToRadians = Math.PI / 180.0;

    public static Reframe None => new(0.0f, 0.0f, 0.0f, 0.0f, Unscaled, 0.0f);

    [JsonIgnore]
    public bool IsNone => this == None;

    public int SceneTilt(float stageTilt) => Std.RoundHalfAway(stageTilt + Tilt);

    public Matrix StageMatrix()
    {
        if (IsNone)
            return Matrix.Identity;

        return MatrixMath.Multiply(MatrixMath.Multiply(Scaling(), Shifting()), Turning());
    }

    private Matrix Scaling()
    {
        float size = Scale > 0.0f ? Scale : Unscaled;
        Matrix scaling = Matrix.Identity;

        for (int k = 0; k < 3; ++k)
            scaling[k * 5] = size;

        return scaling;
    }

    private Matrix Shifting()
    {
        Matrix shifting = Matrix.Identity;
        shifting[12] = -Side;
        shifting[13] = -Height;
        shifting[14] = Distance;

        return shifting;
    }

    private Matrix Turning()
    {
        float angle = (float)(-Turn * DegreesToRadians);
        float cosine = MathF.Cos(angle);
        float sine = MathF.Sin(angle);
        Matrix turning = Matrix.Identity;
        turning[0] = cosine;
        turning[2] = -sine;
        turning[8] = sine;
        turning[10] = cosine;

        return turning;
    }
}
