using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Formats.Pat;

public sealed class PatWriteSprite
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public byte Additive { get; set; }
    public float ZoomX { get; set; } = 1.0f;
    public float ZoomY { get; set; } = 1.0f;
    public int Priority { get; set; }
    public int Part { get; set; }
    public byte[]? Tint { get; set; }
    public float? Turn { get; set; }

    public PatWriteSprite Copy() => (PatWriteSprite)MemberwiseClone();
}

public sealed record PatWritePattern(string Name, IReadOnlyList<PatWriteSprite> Sprites);

public sealed record PatWriteCutout(int Id, string Name, int PivotX, int PivotY, int U, int V, int W, int H, int Width, int Height);

public sealed record PatWriteAtlas(string Name, int Width, int Height, byte[] Rgba);

public static class PatWriter
{
    private const int Body = 0x20;
    private const int AtlasName = 40;
    private const uint FormatArgb = 21;
    private const int DdsSize = 128;

    public static byte[] Build(IReadOnlyList<PatWritePattern> patterns, IReadOnlyList<PatWriteCutout> cutouts, PatWriteAtlas atlas)
    {
        ByteSink sink = new();
        sink.Text("PAniDataFile");
        sink.PadTo(Body);
        sink.Text("_STR");

        for (int index = 0; index < patterns.Count; ++index)
        {
            sink.Text("P_ST");
            sink.Int(index);
            Name(sink, "PANA", patterns[index].Name);

            foreach (PatWriteSprite sprite in patterns[index].Sprites)
                Sprite(sink, sprite);

            sink.Text("P_ED");
        }

        foreach (PatWriteCutout cut in cutouts)
            Cutout(sink, cut, atlas);

        Atlas(sink, atlas);
        sink.Text("_END");

        return sink.ToArray();
    }

    private static void Name(ByteSink sink, string tag, string text)
    {
        byte[] bytes = LittleEndian.Latin1(text);
        sink.Text(tag);
        sink.Byte((byte)bytes.Length);
        sink.Bytes(bytes);
    }

    private static void Sprite(ByteSink sink, PatWriteSprite sprite)
    {
        sink.Text("PRST");
        sink.Int(sprite.Id);
        sink.Text("PRXY");
        sink.Int(sprite.X);
        sink.Int(sprite.Y);
        sink.Text("PRAL");
        sink.Byte(sprite.Additive);
        sink.Text("PRFL");
        sink.Byte(0);
        sink.Text("PRZM");
        sink.Float(sprite.ZoomX);
        sink.Float(sprite.ZoomY);
        sink.Text("PRPR");
        sink.Int(sprite.Priority);
        sink.Text("PRID");
        sink.Int(sprite.Part);

        if (sprite.Tint is not null)
        {
            sink.Text("PRCL");
            sink.Byte(sprite.Tint[2]);
            sink.Byte(sprite.Tint[1]);
            sink.Byte(sprite.Tint[0]);
            sink.Byte(sprite.Tint[3]);
        }

        if (sprite.Turn is not null)
        {
            sink.Text("PRA3");
            sink.Int(0);
            sink.Float(0.0f);
            sink.Float(0.0f);
            sink.Float(sprite.Turn.Value);
        }

        sink.Text("PRED");
    }

    private static void Cutout(ByteSink sink, PatWriteCutout cut, PatWriteAtlas atlas)
    {
        sink.Text("PPST");
        sink.Int(cut.Id);
        Name(sink, "PPNA", cut.Name);
        sink.Text("PPCC");
        sink.Int(cut.PivotX);
        sink.Int(cut.PivotY);
        sink.Text("PPUV");
        sink.Int(cut.U);
        sink.Int(cut.V);
        sink.Int(cut.W);
        sink.Int(cut.H);
        sink.Text("PPSS");
        sink.Int(cut.Width);
        sink.Int(cut.Height);
        sink.Text("PPTP");
        sink.Int(0);
        sink.Text("PPTE");
        sink.Word((ushort)atlas.Width);
        sink.Word((ushort)atlas.Height);
        sink.Text("PPED");
    }

    private static void Atlas(ByteSink sink, PatWriteAtlas atlas)
    {
        int surface = atlas.Width * atlas.Height * 4;
        byte[] name = LittleEndian.Latin1(atlas.Name);

        sink.Text("PGST");
        sink.Int(0);
        sink.Text("PGNM");
        sink.Bytes(name.AsSpan(0, Math.Min(name.Length, AtlasName)));
        sink.Zeros(AtlasName - Math.Min(name.Length, AtlasName));
        sink.Text("PGT2");
        sink.Int(surface + DdsSize);
        sink.Int(atlas.Width);
        sink.Int(atlas.Height);
        sink.Dword(FormatArgb);
        sink.Zeros(8);
        Dds(sink, atlas);
        sink.Text("PGED");
    }

    private static void Dds(ByteSink sink, PatWriteAtlas atlas)
    {
        sink.Text("DDS ");
        sink.Dword(124);
        sink.Dword(0x1 | 0x2 | 0x4 | 0x8 | 0x1000);
        sink.Int(atlas.Height);
        sink.Int(atlas.Width);
        sink.Int(atlas.Width * 4);
        sink.Dword(0);
        sink.Dword(0);
        sink.Zeros(44);
        sink.Dword(32);
        sink.Dword(0x1 | 0x40);
        sink.Dword(0);
        sink.Dword(32);
        sink.Dword(0x00ff0000u);
        sink.Dword(0x0000ff00u);
        sink.Dword(0x000000ffu);
        sink.Dword(0xff000000u);
        sink.Dword(0x1000);
        sink.Zeros(16);

        for (int at = 0; at + 3 < atlas.Rgba.Length; at += 4)
        {
            sink.Byte(atlas.Rgba[at + 2]);
            sink.Byte(atlas.Rgba[at + 1]);
            sink.Byte(atlas.Rgba[at]);
            sink.Byte(atlas.Rgba[at + 3]);
        }
    }
}
