using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Formats.Pat;

public static class PatReader
{
    private const int Body = 0x20;
    private const int Unknown = -1;
    private const long MaxSurface = 64L * 1024 * 1024;
    private const int BlockSide = 4;
    private const long Dxt1Block = 8;
    private const long Dxt5Block = 16;
    private const int DdsBytes = 128;

    private static readonly string[] TopTags = ["P_ST", "PPST", "PGST", "VEST", "_END"];
    private static readonly string[] CutTags = ["PPNA", "PPNM", "PPUV", "PPCC", "PPSS", "PPPA", "PPTP", "PPPP", "PPTE", "PPJP", "PPED"];
    private static readonly string[] AtlasTags = ["PGST", "PPST", "P_ST", "VEST", "_END"];

    public static PatDocument Read(byte[] blob)
    {
        PatDocument document = new();
        int at = ReadPatterns(blob, document);

        while (at + 8 <= blob.Length)
        {
            if (Tag(blob, at, "_END"))
                break;

            if (Tag(blob, at, "PPST"))
            {
                at = ReadCutOut(blob, document, at + 8, LittleEndian.I32(blob, at + 4));
                continue;
            }

            if (Tag(blob, at, "PGST"))
            {
                at = ReadAtlas(blob, document, at + 8, LittleEndian.I32(blob, at + 4));
                continue;
            }

            at = Resync(blob, at, TopTags);
        }

        return document;
    }

    private static bool Tag(byte[] blob, long at, string name) => LittleEndian.TagAt(blob, at, name);

    private static int Resync(byte[] blob, int at, string[] tags)
    {
        for (int probe = at + 4; probe + 4 <= blob.Length; ++probe)
        {
            if (tags.Any(tag => Tag(blob, probe, tag)))
                return probe;
        }

        return blob.Length;
    }

    private static long Blocks(int width, int height, long blockBytes)
    {
        long across = (width + BlockSide - 1) / BlockSide;
        long down = (height + BlockSide - 1) / BlockSide;

        return across * down * blockBytes;
    }

    private static long SurfaceBytes(byte[] blob, int at, int width, int height)
    {
        if (at + 4 > blob.Length)
            return 0;

        if (Tag(blob, at, "DXT5"))
            return Blocks(width, height, Dxt5Block);

        if (Tag(blob, at, "DXT1"))
            return Blocks(width, height, Dxt1Block);

        uint format = LittleEndian.U32(blob, at);
        long pixels = (long)width * height;

        return format switch
        {
            21 or 22 => pixels * 4,
            23 or 25 or 26 => pixels * 2,
            _ => 0,
        };
    }

    private static byte[] RleDecode(byte[] blob, int at, uint packed, uint size)
    {
        byte[] plain = new byte[size];
        long read = at;
        long end = at + packed;
        long write = 0;

        while (read < end && read < blob.Length && write < size)
        {
            byte value = blob[read];

            if (value != 0)
            {
                plain[write++] = value;
                ++read;
                continue;
            }

            if (read + 2 >= end || read + 2 >= blob.Length)
                break;

            long count = Math.Min(blob[read + 2], size - write);
            Array.Fill(plain, blob[read + 1], (int)write, (int)count);
            write += count;
            read += 3;
        }

        return plain;
    }

    private static int ReadAtlas(byte[] blob, PatDocument document, int at, int id)
    {
        int streamAt = 0;
        uint streamLength = 0;
        int width = 0;
        int height = 0;
        byte[] dds = [];

        while (at + 4 <= blob.Length && !Tag(blob, at, "PGED"))
        {
            if (Tag(blob, at, "PGNM"))
            {
                at += 4 + 0x20;
                continue;
            }

            if (!Tag(blob, at, "PGT2"))
            {
                at += 8;
                continue;
            }

            uint packed = LittleEndian.U32(blob, at + 4);
            width = LittleEndian.I32(blob, at + 8);
            height = LittleEndian.I32(blob, at + 12);

            if (width <= 0 || height <= 0)
                break;

            long surface = SurfaceBytes(blob, at + 16, width, height);

            if (surface == 0 || surface + DdsBytes > MaxSurface)
                break;

            if (packed == surface + DdsBytes)
            {
                streamAt = at + 28;
                streamLength = (uint)(surface + DdsBytes);

                if (streamAt + streamLength <= blob.Length)
                    dds = blob.AsSpan(streamAt, (int)streamLength).ToArray();

                break;
            }

            streamLength = LittleEndian.U32(blob, at + 36);
            uint plainLength = LittleEndian.U32(blob, at + 40);
            streamAt = at + 44;

            if (plainLength > DdsBytes && plainLength <= MaxSurface)
                dds = RleDecode(blob, streamAt, streamLength, plainLength);

            break;
        }

        if (dds.Length > 0)
            document.Atlases.Add(new PatAtlas(id, width, height, dds));

        long after = streamAt != 0 && streamLength != 0 ? streamAt + (long)streamLength : at;

        return Resync(blob, (int)Math.Min(after, int.MaxValue - 8), AtlasTags);
    }

    private static string ShortName(byte[] blob, int at, out int length)
    {
        length = at + 4 < blob.Length ? blob[at + 4] : 0;

        if (at + 5 + length > blob.Length)
            return string.Empty;

        return LittleEndian.Ascii(blob, at + 5, length);
    }

    private static int ReadCutOut(byte[] blob, PatDocument document, int at, int id)
    {
        PatPart part = new() { Id = id };

        while (at + 4 <= blob.Length)
        {
            if (Tag(blob, at, "PPED"))
            {
                at += 4;
                break;
            }

            if (Tag(blob, at, "PPNA"))
            {
                part.Name = ShortName(blob, at, out int length);
                at += 5 + length;
                continue;
            }

            if (Tag(blob, at, "PPNM"))
            {
                at += 4 + 0x20;
                continue;
            }

            if (Tag(blob, at, "PPUV"))
            {
                part.U = LittleEndian.I32(blob, at + 4);
                part.V = LittleEndian.I32(blob, at + 8);
                part.W = LittleEndian.I32(blob, at + 12);
                part.H = LittleEndian.I32(blob, at + 16);
                at += 20;
                continue;
            }

            if (Tag(blob, at, "PPCC"))
            {
                part.PivotX = LittleEndian.I32(blob, at + 4);
                part.PivotY = LittleEndian.I32(blob, at + 8);
                at += 12;
                continue;
            }

            if (Tag(blob, at, "PPSS"))
            {
                part.Width = LittleEndian.I32(blob, at + 4);
                part.Height = LittleEndian.I32(blob, at + 8);
                at += 12;
                continue;
            }

            if (Tag(blob, at, "PPTP"))
            {
                part.Atlas = LittleEndian.I32(blob, at + 4);
                at += 8;
                continue;
            }

            if (Tag(blob, at, "PPPA") || Tag(blob, at, "PPTE") || Tag(blob, at, "PPPP"))
            {
                at += 8;
                continue;
            }

            if (Tag(blob, at, "PPJP"))
            {
                at += 12;
                continue;
            }

            at = Resync(blob, at, CutTags);
        }

        document.Parts[id] = part;

        return at;
    }

    private static int SpriteTagSize(byte[] blob, int at)
    {
        if (Tag(blob, at, "PRXY") || Tag(blob, at, "PRZM"))
            return 8;

        if (Tag(blob, at, "PRST") || Tag(blob, at, "PRID") || Tag(blob, at, "PRPR") || Tag(blob, at, "PRCL") || Tag(blob, at, "PRSP"))
            return 4;

        if (Tag(blob, at, "PRFL") || Tag(blob, at, "PRAL") || Tag(blob, at, "PRRV"))
            return 1;

        if (Tag(blob, at, "PRA3"))
            return 16;

        if (Tag(blob, at, "PRED") || Tag(blob, at, "APRC") || Tag(blob, at, "LPRA"))
            return 0;

        return Unknown;
    }

    private static void ReadSpriteTag(byte[] blob, int at, PatSprite sprite)
    {
        if (Tag(blob, at, "PRID"))
        {
            sprite.Part = LittleEndian.I32(blob, at + 4);
        }
        else if (Tag(blob, at, "PRXY"))
        {
            sprite.X = LittleEndian.I32(blob, at + 4);
            sprite.Y = LittleEndian.I32(blob, at + 8);
        }
        else if (Tag(blob, at, "PRZM"))
        {
            sprite.ZoomX = LittleEndian.F32(blob, at + 4);
            sprite.ZoomY = LittleEndian.F32(blob, at + 8);
        }
        else if (Tag(blob, at, "PRCL"))
        {
            sprite.Tint = LittleEndian.U32(blob, at + 4);
        }
        else if (Tag(blob, at, "PRPR"))
        {
            sprite.Priority = LittleEndian.I32(blob, at + 4);
        }
        else if (Tag(blob, at, "PRAL"))
        {
            sprite.Blend = at + 4 < blob.Length ? blob[at + 4] : 0;
        }
        else if (Tag(blob, at, "PRA3"))
        {
            sprite.Pitch = LittleEndian.F32(blob, at + 8);
            sprite.Yaw = LittleEndian.F32(blob, at + 12);
            sprite.Turns = LittleEndian.F32(blob, at + 16);
        }
    }

    private static int ReadPattern(byte[] blob, PatDocument document, int at)
    {
        if (!Tag(blob, at, "PANA"))
            return blob.Length;

        PatPattern pattern = new() { Name = ShortName(blob, at, out int length) };
        document.Patterns.Add(pattern);
        at += 5 + length;

        PatSprite? sprite = null;

        while (at + 4 <= blob.Length)
        {
            if (Tag(blob, at, "P_ED"))
                return at + 4;

            int payload = SpriteTagSize(blob, at);

            if (payload == Unknown)
            {
                ++at;
                continue;
            }

            if (Tag(blob, at, "PRST"))
            {
                sprite = new PatSprite { Id = LittleEndian.I32(blob, at + 4) };
                pattern.Sprites.Add(sprite);
                at += 4 + payload;
                continue;
            }

            if (sprite is not null)
                ReadSpriteTag(blob, at, sprite);

            at += 4 + payload;
        }

        return blob.Length;
    }

    private static int ReadPatterns(byte[] blob, PatDocument document)
    {
        int at = Body;

        if (Tag(blob, at, "_STR"))
            at += 4;

        while (at + 8 <= blob.Length && Tag(blob, at, "P_ST"))
            at = ReadPattern(blob, document, at + 8);

        return at;
    }
}
