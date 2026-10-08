namespace FbxToMua.Core.Formats.FbxText;

internal enum Interpolation
{
    Constant,
    Linear,
    Cubic,
}

internal sealed class CurveKey
{
    public double Time { get; set; }
    public double Value { get; set; }
    public double Right { get; set; }
    public double Left { get; set; }
    public Interpolation Interpolation { get; set; } = Interpolation.Linear;
}

internal sealed class Curve
{
    public bool Present { get; set; }
    public bool Keyed { get; set; }
    public double Fallback { get; set; }
    public List<CurveKey> Keys { get; set; } = [];

    public double Sample(double seconds)
    {
        if (!Keyed || Keys.Count == 0)
            return Fallback;

        if (seconds <= Keys[0].Time)
            return Keys[0].Value;

        if (seconds >= Keys[^1].Time)
            return Keys[^1].Value;

        for (int i = 1; i < Keys.Count; ++i)
        {
            if (seconds > Keys[i].Time)
                continue;

            CurveKey from = Keys[i - 1];
            CurveKey to = Keys[i];
            double span = to.Time - from.Time;

            if (span <= 1e-12)
                return to.Value;

            if (from.Interpolation == Interpolation.Constant)
                return from.Value;

            double blend = (seconds - from.Time) / span;

            if (from.Interpolation == Interpolation.Linear)
                return from.Value + (to.Value - from.Value) * blend;

            return Hermite(from, to, blend, span);
        }

        return Keys[^1].Value;
    }

    private static double Hermite(CurveKey from, CurveKey to, double u, double span)
    {
        double uu = u * u;
        double uuu = uu * u;

        return (2.0 * uuu - 3.0 * uu + 1.0) * from.Value
            + (uuu - 2.0 * uu + u) * span * from.Right
            + (-2.0 * uuu + 3.0 * uu) * to.Value
            + (uuu - uu) * span * from.Left;
    }
}

internal sealed class NodeAnimation
{
    public Curve[] Translation { get; } = [new(), new(), new()];
    public Curve[] Rotation { get; } = [new(), new(), new()];
    public Curve[] Scaling { get; } = [new(), new(), new()];

    public IEnumerable<Curve> All => Translation.Concat(Rotation).Concat(Scaling);

    public bool Keyed => All.Any(curve => curve.Keyed);

    public bool TryKeySpan(out double first, out double last)
    {
        first = 0.0;
        last = 0.0;
        bool any = false;

        for (int i = 0; i < 3; ++i)
        {
            foreach (Curve curve in new[] { Translation[i], Rotation[i], Scaling[i] })
            {
                if (!curve.Keyed || curve.Keys.Count == 0)
                    continue;

                double low = curve.Keys[0].Time;
                double high = curve.Keys[^1].Time;
                first = any && first <= low ? first : low;
                last = any && last >= high ? last : high;
                any = true;
            }
        }

        return any;
    }
}
