using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Formats.Evb;

public static class EvbPlayer
{
    private const int Header = 0x20;
    private const int Record = 0x20;
    private const int Blocks = 0x30;
    private const int SheetBlock = 0;
    private const int NameBlock = 1;
    private const int ParticleBlock = 2;
    private const int RollTries = 16;
    private const int PortRolledFrames = 600;
    private const int LongRun = 600;
    private const int SpriteBudget = 24000;
    private const double LampBend = 1e-3;
    private const int LampRamps = 512;
    private const int MostZones = 8;

    public static bool IsScript(byte[] blob) => blob.Length >= Blocks && LittleEndian.Starts(blob, "EVT0");

    public static EvbPlayed? Play(byte[] blob, string label)
    {
        if (!IsScript(blob))
            return null;

        EvbPlayed played = new() { Sheets = Block(blob, SheetBlock), Named = Block(blob, NameBlock) };
        uint[][] record = Records(blob);

        for (int attempt = 0; attempt < RollTries; ++attempt)
        {
            EvbInstance instance = new(record, attempt == 0 ? label : $"{label} {attempt}");
            instance.Play(played);
            played.Rolled = instance.Rolled;

            if (Shows(played.Sample) || !played.Rolled)
                break;
        }

        return played;
    }

    public static EvbSprite? Sprites(EvbPlayed played)
    {
        int count = played.Sample.Count;

        if (played.Rolled || !played.Cyclic)
            count = Math.Min(count, PortRolledFrames);

        EvbSprite sprite = Collect(played, 0, count);

        if (sprite.Rect.Count > 0 && (long)sprite.Rect.Count * sprite.Frame.Count > SpriteBudget)
            count = Math.Min(count, Math.Max(PortRolledFrames, SpriteBudget / sprite.Rect.Count));

        if (played.Rolled)
        {
            int begin = FirstLit(played);
            sprite = Collect(played, begin, Seam(played, begin, count));
        }
        else if (sprite.Frame.Count != count)
        {
            sprite = Collect(played, 0, count);
        }

        return sprite.Rect.Count > 0 && sprite.Loop > 1 ? sprite : null;
    }

    public static EvbRun? Motions(EvbPlayed played)
    {
        bool takes = played.Named.Any(name => name.EndsWith(".mmot", StringComparison.OrdinalIgnoreCase));
        bool picks = played.Sample.Any(sample => sample.Take >= 0);

        if (!takes || !picks)
            return null;

        List<EvbStep> frames = [];

        foreach (EvbSample sample in played.Sample)
        {
            string take = sample.Take >= 0 && sample.Take < played.Named.Count ? played.Named[(int)sample.Take] : string.Empty;
            frames.Add(new EvbStep(take, (int)sample.Since));
        }

        EvbRun run = new()
        {
            Frame = frames,
            Loop = frames.Count,
            Settled = !played.Cyclic && played.From + 1 == played.Sample.Count,
        };

        return run.Loop > 0 ? run : null;
    }

    public static EvbLamp? Lamps(EvbPlayed played)
    {
        return Timeline(played.Sample.Select(sample => sample.Ramp).ToList(), played.Cyclic ? 0 : played.From);
    }

    public static EvbLamp? Showing(EvbPlayed played, EvbRect rect)
    {
        List<double> level = played.Sample.Select(sample => sample.Lit && sample.Rect == rect ? sample.Ramp : 0.0).ToList();

        return Timeline(level, played.Cyclic ? 0 : played.From);
    }

    public static bool Long(EvbPlayed played)
    {
        if (played.Rolled)
            return false;

        return !played.Cyclic || played.Sample.Count > LongRun;
    }

    public static float? Tilt(byte[] blob)
    {
        if (!IsScript(blob))
            return null;

        bool open = false;

        foreach (long at in RecordOffsets(blob))
        {
            uint code = LittleEndian.U32(blob, at);

            if (code == EvbCode.SceneOpen || code == EvbCode.SceneClose)
            {
                open = code == EvbCode.SceneOpen;
                continue;
            }

            if (open && code == EvbCode.SceneTilt)
                return LittleEndian.I32(blob, at + 4);
        }

        return null;
    }

    public static List<EvbZone> Zones(byte[] blob)
    {
        if (!IsScript(blob))
            return [];

        bool open = false;
        int count = 0;
        EvbZone[] zones = new EvbZone[MostZones];

        foreach (long at in RecordOffsets(blob))
        {
            uint code = LittleEndian.U32(blob, at);

            if (code == EvbCode.SceneOpen || code == EvbCode.SceneClose)
            {
                open = code == EvbCode.SceneOpen;
                continue;
            }

            if (!open)
                continue;

            if (code == EvbCode.ZoneCount)
            {
                count = (int)Math.Min(LittleEndian.U32(blob, at + 4), MostZones);
                continue;
            }

            uint index = LittleEndian.U32(blob, at + 4);

            if (code != EvbCode.Zone || index >= MostZones)
                continue;

            zones[index] = new EvbZone((short)LittleEndian.U32(blob, at + 8), (short)LittleEndian.U32(blob, at + 12));
        }

        return zones.Take(count).ToList();
    }

    public static List<EvbSpawn>? Spawns(byte[] blob)
    {
        if (!IsScript(blob))
            return null;

        List<string> names = Block(blob, ParticleBlock);
        List<EvbSpawn> spawns = [];

        foreach (long at in RecordOffsets(blob))
        {
            if (LittleEndian.U32(blob, at) != EvbCode.Spawn)
                continue;

            int index = LittleEndian.I32(blob, at + 4);

            if (index < 0 || index >= names.Count)
                continue;

            spawns.Add(new EvbSpawn(names[index], LittleEndian.I32(blob, at + 8)));
        }

        return spawns;
    }

    private static IEnumerable<long> RecordOffsets(byte[] blob)
    {
        for (long at = LittleEndian.U32(blob, 0x10); at + Record <= blob.Length; at += Record)
        {
            if (LittleEndian.U32(blob, at) == EvbCode.None)
                yield break;

            yield return at;
        }
    }

    private static uint[][] Records(byte[] blob)
    {
        List<uint[]> records = [];

        for (long at = LittleEndian.U32(blob, 0x10); at + Record <= blob.Length; at += Record)
        {
            uint[] fields = new uint[8];

            for (int k = 0; k < 8; ++k)
                fields[k] = LittleEndian.U32(blob, at + k * 4);

            records.Add(fields);

            if (fields[0] == EvbCode.None)
                break;
        }

        return [.. records];
    }

    private static List<string> Block(byte[] blob, int block)
    {
        List<string> names = [];
        int stride = (int)LittleEndian.U32(blob, 0x14);
        int count = LittleEndian.U16(blob, Header + block * 2);

        if (stride == 0)
            return names;

        long at = Blocks;

        for (int i = 0; i < block; ++i)
            at += LittleEndian.U16(blob, Header + i * 2) * (long)stride;

        for (int i = 0; i < count && at + stride <= blob.Length; ++i, at += stride)
        {
            System.Text.StringBuilder one = new();

            for (int k = 0; k < stride && blob[at + k] != 0; ++k)
            {
                byte letter = blob[at + k];

                if (letter < 0x20 || letter >= 0x7f)
                    return names;

                one.Append((char)letter);
            }

            names.Add(one.ToString());
        }

        return names;
    }

    private static bool Shows(List<EvbSample> samples)
    {
        for (int i = samples.Count > 1 ? 1 : 0; i < samples.Count; ++i)
        {
            if (samples[i].Ramp > 0.0)
                return true;
        }

        return false;
    }

    private static EvbSprite Collect(EvbPlayed played, int begin, int count)
    {
        EvbSprite sprite = new() { Sheets = played.Sheets };

        for (int tick = begin; tick < begin + count && tick < played.Sample.Count; ++tick)
        {
            EvbSample sample = played.Sample[tick];

            if (!sample.Lit)
            {
                sprite.Frame.Add(-1);
                continue;
            }

            int index = sprite.Rect.IndexOf(sample.Rect);

            if (index < 0)
            {
                index = sprite.Rect.Count;
                sprite.Rect.Add(sample.Rect);
            }

            sprite.Frame.Add(index);
        }

        sprite.Loop = sprite.Frame.Count;

        return sprite;
    }

    private static int FirstLit(EvbPlayed played)
    {
        for (int tick = 0; tick < played.Sample.Count; ++tick)
        {
            if (played.Sample[tick].Lit && !played.Sample[tick].Rect.IsSpeck)
                return tick;
        }

        return 0;
    }

    private static int Seam(EvbPlayed played, int begin, int count)
    {
        if (played.Sample.Count == 0)
            return count;

        int last = Math.Min(begin + count, played.Sample.Count - 1);

        for (int end = last; end > begin + count / 2; --end)
        {
            if (played.Sample[end].LooksLike(played.Sample[begin]))
                return end - begin;
        }

        return count;
    }

    private static int Rounded(double value) => (int)Math.Floor(value + 0.5);

    private static EvbLamp? Timeline(List<double> level, int from)
    {
        if (level.Count < 2 || level.All(one => one == level[0]))
            return null;

        int wrap = from >= 0 && from < level.Count ? from : 0;
        List<int> frames = [0];
        List<double> kept = [level[0]];

        for (int tick = 1; tick + 1 < level.Count; ++tick)
        {
            double bend = (level[tick] - level[tick - 1]) - (level[tick + 1] - level[tick]);

            if (!(Math.Abs(bend) > LampBend))
                continue;

            frames.Add(tick);
            kept.Add(level[tick]);
        }

        frames.Add(level.Count - 1);
        kept.Add(level[^1]);
        frames.Add(level.Count);
        kept.Add(level[wrap]);

        EvbLamp lamp = new() { Loop = level.Count, From = wrap };
        int first = Rounded(level[0]);

        if (first != Rounded(level[wrap]))
            lamp.Ramp.Add(new EvbRamp(0, first, 0));

        for (int i = 0; i + 1 < frames.Count; ++i)
            lamp.Ramp.Add(new EvbRamp(frames[i], Rounded(kept[i + 1]), frames[i + 1] - frames[i]));

        return lamp.Ramp.Count > LampRamps ? null : lamp;
    }
}
