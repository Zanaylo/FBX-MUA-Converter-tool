using FbxToMua.Core.Formats.Evb;

namespace FbxToMua.Core.Sources.Mbaa;

internal readonly record struct MbaaSample(int Tick, int Image, float X, float Y, int Alpha)
{
    public bool LooksLike(MbaaSample other) => Image == other.Image && X == other.X && Y == other.Y && Alpha == other.Alpha;
}

internal sealed class MbaaUnit
{
    public int Object { get; init; }
    public int Blend { get; init; }
    public List<MbaaSample> Samples { get; } = [];
}

internal sealed class MbaaTimeline
{
    private const uint Seed = 0x4d424141;
    private const int Warm = 600;
    private const int MostPeriod = 3600;
    private const int Window = 1200;
    private const int MostLoop = 7200;
    private const int MostRecord = 3 * MostLoop;
    private const int MostRamps = 512;
    private const int LampFull = 1000;
    private const int Opaque = 255;
    private const int RampSlack = 4;
    private const int Bits = 64;

    private sealed class Track
    {
        public int Born { get; set; } = -1;
        public bool Ended { get; set; }
        public List<(int Tick, MbaaShown Shown)> Seen { get; } = [];
    }

    public int Period { get; private set; }
    public bool Exact { get; private set; }
    public List<MbaaUnit> Units { get; } = [];

    public static MbaaTimeline? Build(MbaaBg file)
    {
        MbaaTimeline timeline = new();
        int period = FindPeriod(file, out bool rolled);
        timeline.Exact = period > 0 && !rolled;

        MbaaScene scene = new(file, Seed);
        int placed = scene.Instances;

        for (int i = 0; i < Warm; ++i)
            scene.Step();

        int loop = timeline.Exact ? period : Window;
        SortedDictionary<int, Track> tracks = [];

        for (int tick = 0; tick < MostRecord; ++tick)
        {
            Record(scene, tick, tracks);

            if (tick >= loop)
            {
                int longest = Longest(tracks, loop, out bool settled);
                int grown = timeline.Exact ? Grown(period, longest) : loop;

                if (grown > loop)
                    loop = grown;
                else if (settled || tick >= 2 * loop)
                    break;
            }

            scene.Step();
        }

        timeline.Period = loop;

        foreach ((int instance, Track track) in tracks)
        {
            if (track.Seen.Count == 0)
                continue;

            int slot = track.Seen[0].Shown.Object;

            if (instance <= placed && track.Born == 0)
                AddUnits(slot, track.Seen, 0, loop, loop, timeline.Units);
            else if (InWindow(track, loop))
                AddUnits(slot, track.Seen, track.Born, track.Born + loop, loop, timeline.Units);
        }

        return timeline.Units.Count > 0 ? timeline : null;
    }

    public static List<List<int>> Lanes(IReadOnlyList<MbaaUnit> units, int period, int most)
    {
        List<List<int>> free = Assign(units, Enumerable.Range(0, units.Count).ToList(), period, units.Count);

        if (free.Count <= most)
            return free;

        int stride = (free.Count + most - 1) / most;
        List<int> thinned = [];

        for (int i = 0; i < units.Count; i += stride)
            thinned.Add(i);

        return Assign(units, thinned, period, most);
    }

    public static List<EvbRamp>? Ramps(List<int> alpha)
    {
        if (alpha.Count == 0)
            return null;

        List<int> value = Filled(alpha);
        int last = value.Count - 1;
        List<EvbRamp> ramps = [new EvbRamp(0, value[0], 0)];

        for (int from = 0; from < last;)
        {
            int to = from + 1;

            while (to < last && Linear(value, from, to + 1))
                ++to;

            ramps.Add(new EvbRamp(from, value[to], to - from));
            from = to;

            if (ramps.Count > MostRamps)
                return null;
        }

        return ramps;
    }

    private static int FindPeriod(MbaaBg file, out bool rolled)
    {
        MbaaScene probe = new(file, Seed);

        for (int i = 0; i < Warm; ++i)
            probe.Step();

        ulong start = probe.Hash();

        for (int period = 1; period <= MostPeriod; ++period)
        {
            probe.Step();

            if (probe.Hash() != start)
                continue;

            rolled = probe.Rolled;
            return period;
        }

        rolled = probe.Rolled;
        return 0;
    }

    private static bool InWindow(Track track, int loop) => track.Born > 0 && track.Born <= loop;

    private static int Longest(SortedDictionary<int, Track> tracks, int loop, out bool settled)
    {
        int longest = 1;
        settled = true;

        foreach (Track track in tracks.Values)
        {
            if (!InWindow(track, loop) || track.Seen.Count == 0)
                continue;

            settled = settled && track.Ended;
            longest = Math.Max(longest, track.Seen[^1].Tick - track.Born + 1);
        }

        return longest;
    }

    private static int Grown(int period, int longest)
    {
        int loops = (longest + period - 1) / period;

        return Math.Min(MostLoop, Math.Max(period, period * loops));
    }

    private static Track TrackOf(SortedDictionary<int, Track> tracks, int instance)
    {
        if (!tracks.TryGetValue(instance, out Track? track))
            tracks[instance] = track = new Track();

        return track;
    }

    private static void Record(MbaaScene scene, int tick, SortedDictionary<int, Track> tracks)
    {
        List<MbaaShown> shown = scene.Visible();
        List<int> living = scene.Living();
        HashSet<int> alive = [.. living];

        foreach (int instance in living)
        {
            Track track = TrackOf(tracks, instance);

            if (track.Born < 0)
                track.Born = tick;
        }

        foreach (MbaaShown one in shown)
            TrackOf(tracks, one.Instance).Seen.Add((tick, one));

        foreach ((int instance, Track track) in tracks)
            track.Ended = track.Ended || !alive.Contains(instance);
    }

    private static void AddUnits(int slot, List<(int Tick, MbaaShown Shown)> seen, int from, int until, int loop, List<MbaaUnit> units)
    {
        SortedDictionary<int, MbaaUnit> byBlend = [];

        foreach ((int tick, MbaaShown shown) in seen)
        {
            if (tick < from || tick >= until)
                continue;

            if (!byBlend.TryGetValue(shown.Blend, out MbaaUnit? unit))
                byBlend[shown.Blend] = unit = new MbaaUnit { Object = slot, Blend = shown.Blend };

            unit.Samples.Add(new MbaaSample(tick % loop, shown.Image, shown.X, shown.Y, shown.Alpha));
        }

        foreach (MbaaUnit unit in byBlend.Values)
        {
            List<MbaaSample> sorted = unit.Samples.OrderBy(sample => sample.Tick).ToList();
            unit.Samples.Clear();
            unit.Samples.AddRange(sorted);
            units.Add(unit);
        }
    }

    private static bool Linear(List<int> value, int from, int to)
    {
        for (int t = from + 1; t < to; ++t)
        {
            int expected = value[from] + (value[to] - value[from]) * (t - from) / (to - from);

            if (Math.Abs(expected - value[t]) > RampSlack)
                return false;
        }

        return true;
    }

    private static List<int> Filled(List<int> alpha)
    {
        int[] filled = Enumerable.Repeat(-1, alpha.Count).ToArray();
        int held = -1;

        for (int i = 0; i < alpha.Count * 2; ++i)
        {
            int value = alpha[i % alpha.Count];

            if (value >= 0)
                held = value * LampFull / Opaque;

            if (filled[i % alpha.Count] < 0)
                filled[i % alpha.Count] = held;
        }

        return filled.Select(value => value < 0 ? LampFull : value).ToList();
    }

    private static bool Overlaps(ulong[] busy, MbaaUnit unit) => unit.Samples.Any(sample => ((busy[sample.Tick / Bits] >> (sample.Tick % Bits)) & 1u) != 0);

    private static void Occupy(ulong[] busy, MbaaUnit unit)
    {
        foreach (MbaaSample sample in unit.Samples)
            busy[sample.Tick / Bits] |= 1ul << (sample.Tick % Bits);
    }

    private static List<List<int>> Assign(IReadOnlyList<MbaaUnit> units, List<int> order, int period, int most)
    {
        List<List<int>> lanes = [];
        List<ulong[]> busy = [];
        int words = period / Bits + 1;

        foreach (int index in order)
        {
            MbaaUnit unit = units[index];
            int lane = 0;

            while (lane < lanes.Count && Overlaps(busy[lane], unit))
                ++lane;

            if (lane == lanes.Count)
            {
                if (lanes.Count >= most)
                    continue;

                lanes.Add([]);
                busy.Add(new ulong[words]);
            }

            lanes[lane].Add(index);
            Occupy(busy[lane], unit);
        }

        return lanes;
    }
}
