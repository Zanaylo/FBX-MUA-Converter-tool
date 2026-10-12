using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public static class SceneLight
{
    private const float Thousandths = 1000.0f;
    private const float ShortestShadow = 0.02f;
    private const double DegreesToRadians = Math.PI / 180.0;

    public static int[] Vector(Reframe reframe)
    {
        double turn = reframe.ShadowTurn * DegreesToRadians;
        float length = Math.Max(reframe.ShadowLength, ShortestShadow);
        float scale = Thousandths / Math.Max(1.0f, length);

        return
        [
            Std.RoundHalfAway((float)(-Math.Sin(turn) * length * scale)),
            Std.RoundHalfAway(scale),
            Std.RoundHalfAway((float)(-Math.Cos(turn) * length * scale)),
        ];
    }

    public static (float X, float Z) Reach(Reframe reframe, float height)
    {
        int[] light = Vector(reframe);
        float along = height / light[1];

        return (light[0] * along, light[2] * along);
    }

    public static int Strength(Reframe reframe) => Std.RoundHalfAway(reframe.ShadowStrength * Thousandths);
}
