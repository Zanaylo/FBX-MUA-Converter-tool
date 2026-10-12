namespace FbxToMua.Core.Export;

public readonly record struct ShadowBand(float Low, float High)
{
    public static ShadowBand Open => new(float.NegativeInfinity, float.PositiveInfinity);

    public bool IsOpen => float.IsNegativeInfinity(Low) && float.IsPositiveInfinity(High);

    public bool Holds(float low, float high) => low >= Low && high <= High;
}

public static class ShadowArea
{
    private const double AreaWidthPixels = 4096.0;
    private const double AreaDepthPixels = 3072.0;
    private const double Half = 0.5;
    private const double Reach = BattleCamera.FarPlane * 10.0;
    private const int Halvings = 64;
    private const float Margin = 0.99f;
    private const int X = 0;
    private const int Y = 1;
    private const int Z = 2;

    private static readonly double UnitsPerPixel = 1.0 / BattleCamera.LayerUnits();
    private static readonly double HalfWidth = AreaWidthPixels * UnitsPerPixel * Half;
    private static readonly double Depth = AreaDepthPixels * UnitsPerPixel;

    private readonly record struct Light(float Z, double Across, double Rise);

    public static bool Takes(int[] light, float lowY, float lowZ, float highZ)
    {
        Light lit = LightOf(light);

        if (lowY > Depth)
            return false;

        return HighFits(lit, highZ) && LowFits(lit, lowZ);
    }

    public static ShadowBand Band(int[] light)
    {
        Light lit = LightOf(light);
        double high = Edge(depth => HighFits(lit, depth), 1.0);
        double low = Edge(depth => LowFits(lit, depth), -1.0);

        return new ShadowBand((float)(low * Margin), (float)(high * Margin));
    }

    private static Light LightOf(int[] light)
    {
        float x = light[X];
        float y = light[Y];
        float z = light[Z];

        return new Light(z, z / x, y / z);
    }

    private static double Edge(Func<double, bool> fits, double side)
    {
        if (fits(Reach * side))
            return double.PositiveInfinity * side;

        double inside = 0.0;
        double outside = Reach;

        for (int i = 0; i < Halvings; ++i)
        {
            double middle = (inside + outside) * Half;

            if (fits(middle * side))
                inside = middle;
            else
                outside = middle;
        }

        return inside * side;
    }

    private static bool HighFits(Light light, double high)
    {
        if (light.Z > 0.0f)
            return !(0.0 > high) && !(high > HalfWidth + light.Across * high);

        if (light.Z < 0.0f)
            return !(high > HalfWidth - light.Across * high);

        return !(-light.Rise * high > high);
    }

    private static bool LowFits(Light light, double low)
    {
        if (light.Z > 0.0f)
            return !(light.Across * low - HalfWidth > low);

        if (light.Z < 0.0f)
            return !(low > 0.0) && !(-light.Across * low - HalfWidth > low);

        return !(low > Depth - light.Rise * low);
    }
}
