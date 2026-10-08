using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Fpac;

namespace FbxToMua.Core.Import;

public readonly record struct ParticleRange(double Min, double Max);

public readonly record struct ParticleSize(ParticleRange Width, ParticleRange Height);

public sealed class ParticleGroup
{
    public uint Flags { get; init; }
    public int CountMin { get; init; }
    public int CountMax { get; init; }
    public int LifeMin { get; init; }
    public int LifeMax { get; init; }
    public int DelayMin { get; init; }
    public int DelayMax { get; init; }
    public int ChildStart { get; init; }
}

public sealed class ParticleSprite
{
    public uint Flags { get; init; }
    public uint Flags2 { get; init; }
    public ParticleRange RotationStart { get; init; }
    public ParticleRange RotationSpeed { get; init; }
    public ParticleRange RotationAccel { get; init; }
    public double RotationDamping { get; init; }
    public int ColourPeriodMin { get; init; }
    public int ColourPeriodMax { get; init; }
    public uint ColourStart { get; init; }
    public uint ColourMid { get; init; }
    public uint ColourEnd { get; init; }
    public double ColourMidAt { get; init; }
    public int ScalePeriodMin { get; init; }
    public int ScalePeriodMax { get; init; }
    public ParticleSize ScaleStart { get; init; }
    public ParticleSize ScaleMid { get; init; }
    public ParticleSize ScaleEnd { get; init; }
    public double ScaleMidAt { get; init; }
    public int UvAnime { get; init; }
    public double[] Uv { get; init; } = new double[4];
    public int SharedAtlas { get; init; }
}

public sealed class ParticleShape
{
    public uint Flags { get; init; }
    public double Radius { get; init; }
    public double AngleRange { get; init; }
    public double AngleOffset { get; init; }
    public double[] BoxMin { get; init; } = new double[3];
    public double[] BoxMax { get; init; } = new double[3];
    public double SizeScale { get; init; }
}

public sealed class ParticleMove
{
    public uint Flags { get; init; }
    public ParticleRange[] Velocity { get; init; } = new ParticleRange[3];
    public ParticleRange[] Accel { get; init; } = new ParticleRange[3];
    public double Damping { get; init; }
}

public sealed record ParticleEffect(string Name, ParticleGroup Group, ParticleSprite Sprite, ParticleShape Shape, ParticleMove Move);

public sealed record ParticleSurface(int Width, int Height, byte[] Bgra);

public static class Particles
{
    private const uint Version = 0x102;
    private const int Header = 16;
    private const int NameBytes = 32;
    private const int Entry = 0x48;
    private const int SpriteBytes = 0xb0;
    private const int ShapeBytes = 0x38;
    private const int MoveBytes = 0xa0;
    private const int DdsPixels = 128;
    private const int DdsBitsAt = 88;
    private const int DdsMasksAt = 92;
    private const uint DdsBits = 32;
    private static readonly uint[] DdsMasks = [0x00ff0000u, 0x0000ff00u, 0x000000ffu, 0xff000000u];

    private readonly struct Reader(byte[] blob, long baseAt)
    {
        public bool Fits(int size) => baseAt >= 0 && baseAt + size <= blob.Length;

        public uint Dword(int at) => LittleEndian.U32(blob, baseAt + at);

        public int Int(int at) => (int)Dword(at);

        public double Float(int at) => LittleEndian.F32(blob, baseAt + at);

        public ParticleRange Pair(int maxAt, int minAt) => new(Float(minAt), Float(maxAt));
    }

    public static List<ParticleEffect>? Read(byte[] pac)
    {
        SortedDictionary<string, byte[]> files = Fpac.Walk(pac);
        byte[]? table = Fpac.Named(files, "particle.bin");
        byte[]? named = Fpac.Named(files, "ptlname.bin");

        if (table is null || named is null)
            return null;

        List<string>? names = Names(named);
        uint? count = Counted(table);

        if (names is null || count is null || count != names.Count)
            return null;

        List<ParticleEffect> effects = [];

        for (int i = 0; i < count; ++i)
        {
            ParticleEffect? effect = EffectAt(table, i, names[i]);

            if (effect is null)
                return null;

            effects.Add(effect);
        }

        return effects;
    }

    public static ParticleSurface? Atlas(byte[] pac)
    {
        byte[]? dds = Fpac.Named(Fpac.Walk(pac), "particle.dds");
        ArtSize? size = dds is null ? null : ArtSheet.Measure(dds);

        if (dds is null || size is null || LittleEndian.U32(dds, DdsBitsAt) != DdsBits)
            return null;

        for (int i = 0; i < 4; ++i)
        {
            if (LittleEndian.U32(dds, DdsMasksAt + i * 4) != DdsMasks[i])
                return null;
        }

        int bytes = size.Value.Width * size.Value.Height * 4;

        return DdsPixels + bytes > dds.Length ? null : new ParticleSurface(size.Value.Width, size.Value.Height, dds.AsSpan(DdsPixels, bytes).ToArray());
    }

    public static ParticleEffect? Find(IReadOnlyList<ParticleEffect> effects, string name) => effects.FirstOrDefault(effect => effect.Name == name);

    private static uint? Counted(byte[] blob)
    {
        if (blob.Length < Header || !LittleEndian.Starts(blob, "LTP ") || LittleEndian.U32(blob, 4) != Version)
            return null;

        return LittleEndian.U32(blob, 8);
    }

    private static List<string>? Names(byte[] blob)
    {
        uint? count = Counted(blob);

        if (count is null || Header + count * (long)NameBytes > blob.Length)
            return null;

        return Enumerable.Range(0, (int)count).Select(i => LittleEndian.Ascii(blob, Header + i * NameBytes, NameBytes)).ToList();
    }

    private static ParticleSize SizeAt(Reader at, int baseAt) => new(at.Pair(baseAt, baseAt + 8), at.Pair(baseAt + 4, baseAt + 12));

    private static ParticleSprite SpriteAt(Reader at)
    {
        return new ParticleSprite
        {
            Flags = at.Dword(0x00),
            Flags2 = at.Dword(0x04),
            RotationStart = at.Pair(0x10, 0x14),
            RotationSpeed = at.Pair(0x18, 0x1c),
            RotationAccel = at.Pair(0x20, 0x24),
            RotationDamping = at.Float(0x28),
            ColourPeriodMax = at.Int(0x3c),
            ColourPeriodMin = at.Int(0x40),
            ColourStart = at.Dword(0x44),
            ColourEnd = at.Dword(0x48),
            ColourMid = at.Dword(0x4c),
            ColourMidAt = at.Float(0x50),
            ScalePeriodMax = at.Int(0x54),
            ScalePeriodMin = at.Int(0x58),
            ScaleStart = SizeAt(at, 0x5c),
            ScaleMid = SizeAt(at, 0x6c),
            ScaleEnd = SizeAt(at, 0x7c),
            ScaleMidAt = at.Float(0x8c),
            UvAnime = at.Int(0x94),
            Uv = [at.Float(0x98), at.Float(0x9c), at.Float(0xa0), at.Float(0xa4)],
            SharedAtlas = at.Int(0xac),
        };
    }

    private static ParticleShape ShapeAt(Reader at)
    {
        return new ParticleShape
        {
            Flags = at.Dword(0x00),
            Radius = at.Float(0x08),
            AngleRange = at.Float(0x0c),
            AngleOffset = at.Float(0x10),
            BoxMin = [at.Float(0x14), at.Float(0x18), at.Float(0x1c)],
            BoxMax = [at.Float(0x20), at.Float(0x24), at.Float(0x28)],
            SizeScale = at.Float(0x30),
        };
    }

    private static ParticleMove MoveAt(Reader at)
    {
        return new ParticleMove
        {
            Flags = at.Dword(0x00),
            Velocity = Enumerable.Range(0, 3).Select(axis => at.Pair(0x08 + axis * 4, 0x14 + axis * 4)).ToArray(),
            Accel = Enumerable.Range(0, 3).Select(axis => at.Pair(0x40 + axis * 4, 0x4c + axis * 4)).ToArray(),
            Damping = at.Float(0x58),
        };
    }

    private static int EntryIndex(byte[] blob, uint offset)
    {
        if (offset < Header || (offset - Header) % Entry != 0 || offset + (long)Entry > blob.Length)
            return -1;

        return (int)((offset - Header) / Entry);
    }

    private static ParticleEffect? EffectAt(byte[] blob, int index, string name)
    {
        Reader entry = new(blob, Header + (long)index * Entry);

        if (!entry.Fits(Entry))
            return null;

        ParticleGroup group = new()
        {
            Flags = entry.Dword(0x00),
            CountMax = entry.Int(0x08),
            CountMin = entry.Int(0x0c),
            LifeMax = entry.Int(0x10),
            LifeMin = entry.Int(0x14),
            DelayMax = entry.Int(0x18),
            DelayMin = entry.Int(0x1c),
            ChildStart = EntryIndex(blob, entry.Dword(0x38)),
        };

        Reader sprite = new(blob, entry.Dword(0x2c));
        Reader shape = new(blob, entry.Dword(0x30));
        Reader move = new(blob, entry.Dword(0x34));

        if (!sprite.Fits(SpriteBytes) || !shape.Fits(ShapeBytes) || !move.Fits(MoveBytes))
            return null;

        return new ParticleEffect(name, group, SpriteAt(sprite), ShapeAt(shape), MoveAt(move));
    }
}
