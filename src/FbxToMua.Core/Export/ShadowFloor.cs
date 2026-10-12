using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public static class ShadowFloor
{
    private const float GroundReach = 10.0f;
    private const float FightReach = 1500.0f;
    private const float FightStep = 50.0f;
    private const double Upright = 0.866;
    private const double GroundShare = 0.5;
    private const double ParallelogramToTriangle = 0.5;
    private const int Corners = 3;
    private const int X = 0;
    private const int Y = 1;
    private const int Z = 2;

    private static readonly float[] FightLine = Enumerable.Range(0, (int)(2.0f * FightReach / FightStep) + 1).Select(step => -FightReach + step * FightStep).ToArray();

    public static bool Catches(IReadOnlyList<Float3> positions, IReadOnlyList<int> triangles)
    {
        double upright = 0.0;
        double total = 0.0;
        bool underFeet = false;

        for (int i = 0; i + Corners - 1 < triangles.Count; i += Corners)
        {
            Float3 a = positions[triangles[i]];
            Float3 b = positions[triangles[i + 1]];
            Float3 c = positions[triangles[i + 2]];
            (double area, double rise) = AreaAndRise(a, b, c);
            total += area;

            if (area <= 0.0 || rise < Upright * area)
                continue;

            upright += area;
            underFeet = underFeet || UnderTheFighters(a, b, c);
        }

        return underFeet && upright >= total * GroundShare;
    }

    private static bool UnderTheFighters(Float3 a, Float3 b, Float3 c)
    {
        foreach (float x in FightLine)
        {
            if (HeightAt(a, b, c, x) is float height && MathF.Abs(height) <= GroundReach)
                return true;
        }

        return false;
    }

    private static float? HeightAt(Float3 a, Float3 b, Float3 c, float x)
    {
        float span = (b[Z] - c[Z]) * (a[X] - c[X]) + (c[X] - b[X]) * (a[Z] - c[Z]);

        if (span == 0.0f)
            return null;

        float first = ((b[Z] - c[Z]) * (x - c[X]) - (c[X] - b[X]) * c[Z]) / span;
        float second = ((c[Z] - a[Z]) * (x - c[X]) - (a[X] - c[X]) * c[Z]) / span;
        float third = 1.0f - first - second;

        if (first < 0.0f || second < 0.0f || third < 0.0f)
            return null;

        return first * a[Y] + second * b[Y] + third * c[Y];
    }

    private static (double Area, double Rise) AreaAndRise(Float3 a, Float3 b, Float3 c)
    {
        double ux = b[X] - a[X], uy = b[Y] - a[Y], uz = b[Z] - a[Z];
        double vx = c[X] - a[X], vy = c[Y] - a[Y], vz = c[Z] - a[Z];
        double nx = uy * vz - uz * vy;
        double ny = uz * vx - ux * vz;
        double nz = ux * vy - uy * vx;

        return (Math.Sqrt(nx * nx + ny * ny + nz * nz) * ParallelogramToTriangle, Math.Abs(ny) * ParallelogramToTriangle);
    }
}
