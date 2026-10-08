using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public static class KeyReduction
{
    private const float MoveTolerance = 1e-3f;
    private const float TurnTolerance = 1e-5f;
    private const float ScaleTolerance = 1e-5f;
    private const int LongestSpan = 256;

    public static List<int> Kept(IReadOnlyList<Pose> poses)
    {
        List<int> kept = [0];
        int last = poses.Count - 1;
        int from = 0;

        while (from < last)
        {
            int to = from + 1;

            while (to < last && to + 1 - from <= LongestSpan && Fits(poses, from, to + 1))
                ++to;

            kept.Add(to);
            from = to;
        }

        return kept;
    }

    public static Float4 Slerp(Float4 from, Float4 to, float t)
    {
        Float4 target = to;
        float cosine = from.Dot(target);

        if (cosine < 0.0f)
        {
            cosine = -cosine;
            target = target.Negated();
        }

        float low = 1.0f - t;
        float high = t;

        if (cosine < 0.9999f)
        {
            float angle = MathF.Acos(cosine);
            float sine = MathF.Sin(angle);
            low = MathF.Sin((1.0f - t) * angle) / sine;
            high = MathF.Sin(t * angle) / sine;
        }

        Float4 turned = new();

        for (int k = 0; k < 4; ++k)
            turned[k] = from[k] * low + target[k] * high;

        return turned;
    }

    private static bool Close(ReadOnlySpan<float> left, ReadOnlySpan<float> right, float tolerance)
    {
        for (int k = 0; k < left.Length; ++k)
        {
            if (MathF.Abs(left[k] - right[k]) > tolerance)
                return false;
        }

        return true;
    }

    private static bool Fits(IReadOnlyList<Pose> poses, int from, int to)
    {
        Pose a = poses[from];
        Pose b = poses[to];

        for (int k = from + 1; k < to; ++k)
        {
            float t = (float)(k - from) / (to - from);
            Pose actual = poses[k];
            Float3 moved = new();
            Float3 sized = new();

            for (int c = 0; c < 3; ++c)
            {
                moved[c] = a.Translation[c] + (b.Translation[c] - a.Translation[c]) * t;
                sized[c] = a.Scale[c] + (b.Scale[c] - a.Scale[c]) * t;
            }

            Float4 turned = Slerp(a.Turn, b.Turn, t);

            if (turned.Dot(actual.Turn) < 0.0f)
                turned = turned.Negated();

            if (!Close(moved, actual.Translation, MoveTolerance) || !Close(sized, actual.Scale, ScaleTolerance)
                || !Close(turned, actual.Turn, TurnTolerance))
            {
                return false;
            }
        }

        return true;
    }
}
