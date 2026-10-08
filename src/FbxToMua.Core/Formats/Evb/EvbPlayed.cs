namespace FbxToMua.Core.Formats.Evb;

public readonly record struct EvbRect(int Sheet, int X, int Y, int W, int H)
{
    public const int Least = 4;

    public bool IsSpeck => W <= Least || H <= Least;
}

public readonly record struct EvbSample(bool Lit, EvbRect Rect, double Ramp, long Take, long Since)
{
    public bool LooksLike(EvbSample other)
    {
        if (Lit != other.Lit || Ramp != other.Ramp)
            return false;

        return !Lit || Rect == other.Rect;
    }

    public bool SameAs(EvbSample other) => LooksLike(other) && Take == other.Take;
}

public sealed class EvbPlayed
{
    public List<EvbSample> Sample { get; set; } = [];
    public List<string> Sheets { get; init; } = [];
    public List<string> Named { get; init; } = [];
    public bool Cyclic { get; set; }
    public bool Rolled { get; set; }
    public int From { get; set; }
}

public sealed class EvbSprite
{
    public int Loop { get; set; }
    public List<int> Frame { get; } = [];
    public List<EvbRect> Rect { get; } = [];
    public List<string> Sheets { get; init; } = [];
}

public readonly record struct EvbStep(string Take, int At);

public sealed class EvbRun
{
    public int Loop { get; init; }
    public bool Settled { get; init; }
    public List<EvbStep> Frame { get; init; } = [];
}

public readonly record struct EvbRamp(int At, int Target, int Frames);

public sealed class EvbLamp
{
    public int Loop { get; set; }
    public int From { get; set; }
    public List<EvbRamp> Ramp { get; } = [];
}

public sealed class EvbFlip
{
    public List<float> Rects { get; } = [];
    public List<int> Frame { get; } = [];
}

public readonly record struct EvbSpawn(string Effect, int Bone);

public readonly record struct EvbZone(int Limit, int Kind);
