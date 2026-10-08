using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Sources.Mbaa;

internal enum MbaaBlend
{
    Solid = 0,
    Add = 1,
    AddStrong = 2,
    Translucent = 3,
}

internal static class MbaaOp
{
    public const int End = 0;
    public const int Next = 1;
    public const int Jump = 2;
    public const int NextToo = 3;
    public const int JumpToo = 4;
    public const int Count = 5;
}

internal static class MbaaEvent
{
    public const int Spawn = 1;
    public const int SpawnAnywhere = 2;
    public const int Velocity = 100;
    public const int Position = 1;
}

internal sealed class MbaaRecord
{
    public int Kind { get; init; }
    public int Target { get; init; }
    public int[] Values { get; init; } = new int[MbaaBg.RecordValues];
}

internal sealed class MbaaFrame
{
    public int Image { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Duration { get; init; }
    public int Blend { get; init; }
    public int Alpha { get; init; }
    public int Op { get; init; }
    public int Jump { get; init; }
    public int Tween { get; init; }
    public int Exit { get; init; }
    public int Loops { get; init; }
    public bool StopX { get; init; }
    public bool StopY { get; init; }
    public bool PushX { get; init; }
    public bool PushY { get; init; }
    public int[] Velocity { get; init; } = new int[2];
    public int[] Acceleration { get; init; } = new int[2];
    public List<int> Conditions { get; init; } = [];
    public List<int> Events { get; init; } = [];

    public bool IsSprite => Image >= MbaaBg.SpriteBase;

    public int AlphaValue => Blend == (int)MbaaBlend.Solid ? MbaaBg.Opaque : Alpha;
}

internal sealed class MbaaLayer
{
    public int Object { get; init; }
    public int Parallax { get; init; } = MbaaBg.FullParallax;
    public int Priority { get; init; }
    public bool Placed { get; init; } = true;
    public List<MbaaFrame> Frames { get; } = [];
    public List<MbaaRecord> Events { get; set; } = [];
    public List<MbaaRecord> Conditions { get; set; } = [];
}

internal sealed class MbaaBg
{
    public const int SpriteBase = 10000;
    public const int FullParallax = 256;
    public const int RecordValues = 6;
    public const int Opaque = 255;

    private const string Magic = "bgmake";
    private const int CgOffsetAt = 0x1c;
    private const int CgBytesAt = 0x20;
    private const int ObjectTableAt = 0x54;
    private const int ObjectSlots = 256;
    private const int ConditionsAt = 0x0c;
    private const int EventsAt = 0x10;
    private const int PlacedAt = 0x14;
    private const int LayerHeader = 0x3c;
    private const int FrameBytes = 0x84;
    private const int RecordBytes = 0x34;
    private const int MostFrames = 256;
    private const int References = 8;
    private const int MotionAt = 0x2c;
    private const int VelocityAt = 0x34;
    private const int AccelerationAt = 0x3c;
    private const int ConditionListAt = 0x64;
    private const int EventListAt = 0x74;

    public List<MbaaLayer> Layers { get; } = [];
    public int CgAt { get; private init; }
    public int CgBytes { get; private init; }

    public static MbaaBg? Read(byte[] dat)
    {
        if (dat.Length < ObjectTableAt + ObjectSlots * 4 || !LittleEndian.Starts(dat, Magic))
            return null;

        MbaaBg file = new() { CgAt = (int)LittleEndian.U32(dat, CgOffsetAt), CgBytes = (int)LittleEndian.U32(dat, CgBytesAt) };

        if (file.CgAt == 0 || (long)file.CgAt + file.CgBytes > dat.Length)
            return null;

        for (int slot = 0; slot < ObjectSlots; ++slot)
        {
            uint at = LittleEndian.U32(dat, ObjectTableAt + slot * 4);

            if (at == 0xffffffffu)
                continue;

            MbaaLayer? layer = ReadLayer(dat, slot, at);

            if (layer is not null)
                file.Layers.Add(layer);
        }

        return file.Layers.Count > 0 ? file : null;
    }

    public MbaaLayer? LayerOf(int slot) => Layers.FirstOrDefault(layer => layer.Object == slot);

    private static int Short(byte[] dat, long at) => at + 2 <= dat.Length ? (short)LittleEndian.U16(dat, at) : 0;

    private static int Word(byte[] dat, long at) => LittleEndian.U16(dat, at);

    private static byte ByteAt(byte[] dat, long at) => at < dat.Length ? dat[at] : (byte)0;

    private static List<int> ReferencesAt(byte[] dat, long at)
    {
        List<int> references = [];

        for (int i = 0; i < References; ++i)
        {
            int reference = Short(dat, at + i * 2);

            if (reference >= 0)
                references.Add(reference);
        }

        return references;
    }

    private static MbaaFrame? ReadFrame(byte[] dat, long at)
    {
        if (at + FrameBytes > dat.Length)
            return null;

        return new MbaaFrame
        {
            Image = Word(dat, at),
            X = Short(dat, at + 2),
            Y = Short(dat, at + 4),
            Duration = Word(dat, at + 6),
            Blend = dat[at + 9],
            Alpha = dat[at + 10],
            Op = dat[at + 11],
            Jump = dat[at + 12],
            Tween = dat[at + 0x14],
            Exit = dat[at + 0x15],
            Loops = dat[at + 0x16],
            StopX = dat[at + MotionAt] != 0,
            StopY = dat[at + MotionAt + 1] != 0,
            PushX = dat[at + MotionAt + 2] != 0,
            PushY = dat[at + MotionAt + 3] != 0,
            Velocity = [Short(dat, at + VelocityAt), Short(dat, at + VelocityAt + 2)],
            Acceleration = [Short(dat, at + AccelerationAt), Short(dat, at + AccelerationAt + 2)],
            Conditions = ReferencesAt(dat, at + ConditionListAt),
            Events = ReferencesAt(dat, at + EventListAt),
        };
    }

    private static List<MbaaRecord> ReadRecords(byte[] dat, long at, int count)
    {
        List<MbaaRecord> records = [];

        for (int i = 0; i < count; ++i)
        {
            long record = at + (long)i * RecordBytes;

            if (record + RecordBytes > dat.Length)
                break;

            records.Add(new MbaaRecord
            {
                Kind = Short(dat, record),
                Target = Short(dat, record + 2),
                Values = Enumerable.Range(0, RecordValues).Select(v => (int)LittleEndian.U32(dat, record + 4 + v * 4)).ToArray(),
            });
        }

        return records;
    }

    private static MbaaLayer? ReadLayer(byte[] dat, int slot, long at)
    {
        if (at + LayerHeader > dat.Length)
            return null;

        int count = (int)LittleEndian.U32(dat, at);
        MbaaLayer layer = new()
        {
            Object = slot,
            Parallax = (int)LittleEndian.U32(dat, at + 4),
            Priority = (int)LittleEndian.U32(dat, at + 8),
            Placed = ByteAt(dat, at + PlacedAt) == 0,
        };

        for (int i = 0; i < count && i < MostFrames; ++i)
        {
            MbaaFrame? frame = ReadFrame(dat, at + LayerHeader + (long)i * FrameBytes);

            if (frame is null)
                break;

            layer.Frames.Add(frame);
        }

        int mostEvent = layer.Frames.SelectMany(frame => frame.Events).DefaultIfEmpty(-1).Max();
        int mostCondition = layer.Frames.SelectMany(frame => frame.Conditions).DefaultIfEmpty(-1).Max();
        layer.Events = ReadRecords(dat, at + LittleEndian.U32(dat, at + EventsAt), mostEvent + 1);
        layer.Conditions = ReadRecords(dat, at + LittleEndian.U32(dat, at + ConditionsAt), mostCondition + 1);

        return layer.Frames.Count > 0 ? layer : null;
    }
}
