using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public struct DepthSpan
{
    public double Nearest;
    public double Farthest;

    public static DepthSpan Empty => new() { Nearest = double.MaxValue, Farthest = -double.MaxValue };

    public void Widen(Float3 point)
    {
        double depth = FarField.Depth(point);

        if (depth <= 0.0)
            return;

        Nearest = Std.Min(Nearest, depth);
        Farthest = Std.Max(Farthest, depth);
    }
}

public readonly record struct FarPull(bool Pulled, bool KeepsDepth, float Factor);

public static class FarField
{
    public const double Reach = BattleCamera.FarPlane * 0.95;
    public const double Distant = BattleCamera.FarPlane * 0.2;

    public static double Depth(Float3 point) => point[2] + BattleCamera.EyeDistance;

    public static List<FarPull> Decide(IReadOnlyList<DepthSpan> spans)
    {
        double backmost = Backmost(spans);
        List<FarPull> pulls = new(spans.Count);

        foreach (DepthSpan span in spans)
        {
            if (!Pullable(span))
            {
                pulls.Add(new FarPull(false, true, 1.0f));
                continue;
            }

            double factor = Reach / span.Farthest;
            pulls.Add(new FarPull(true, factor * span.Nearest >= backmost, (float)factor));
        }

        return pulls;
    }

    public static Matrix Pull(float factor)
    {
        Matrix pull = Matrix.Identity;
        Float3 eye = new(0.0f, (float)BattleCamera.EyeHeight, -(float)BattleCamera.EyeDistance);

        for (int c = 0; c < 3; ++c)
        {
            pull[c * 5] = factor;
            pull[12 + c] = eye[c] * (1.0f - factor);
        }

        return pull;
    }

    private static bool Pullable(DepthSpan span) => span.Farthest > Reach && span.Nearest >= Distant;

    private static double Backmost(IReadOnlyList<DepthSpan> spans)
    {
        double backmost = 0.0;

        foreach (DepthSpan span in spans)
        {
            if (!Pullable(span))
                backmost = Std.Max(backmost, Std.Min(span.Farthest, BattleCamera.FarPlane));
        }

        return backmost;
    }
}
