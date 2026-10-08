namespace FbxToMua.Core.Geometry;

public static class Std
{
    public static float Min(float left, float right) => right < left ? right : left;

    public static float Max(float left, float right) => left < right ? right : left;

    public static double Min(double left, double right) => right < left ? right : left;

    public static double Max(double left, double right) => left < right ? right : left;

    public static long RoundHalfAway(double value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);

    public static int RoundHalfAway(float value) => (int)MathF.Round(value, MidpointRounding.AwayFromZero);
}
