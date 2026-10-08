using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;

namespace FbxToMua.Core.Import;

internal readonly record struct Sheeted(int Sheet, ArtSize? Size, EvbRect Rect);

internal sealed class ArcAtlas(FbxExModel built, SortedDictionary<string, byte[]> images)
{
    private const string CaptureSheet = "capture";
    private const string CaptureTexture = "uni2im_capture.dds";
    private const string CaptureStamp = "UNI2IM:CAPTURE";
    private const int CaptureStampAt = 32;
    private const uint CardSide = 64;
    private const uint CardBytes = CardSide * CardSide / 2;
    private const string BannerSheet = "stagefont";
    private const int BannerLines = 2;

    private readonly List<byte[]?> _pixels = [];
    private readonly List<bool> _cutout = [];
    private readonly List<ArtSheet> _sheets = [];
    private readonly Dictionary<(int, int), int> _retextured = [];
    private int _captureAt = -1;

    public int Count => built.Textures.Count;

    public int Add(string name)
    {
        string leaf = ArcMotion.Lowered(LeafOf(name));
        bool known = images.TryGetValue(leaf, out byte[]? image);

        built.Textures.Add(known ? leaf : ArcMotion.Lowered(name));
        _pixels.Add(known ? image : null);
        _cutout.Add(ArtSheet.Transparent(image ?? []));
        _sheets.Add(new ArtSheet(image ?? []));

        return built.Textures.Count - 1;
    }

    public int Find(string name)
    {
        string leaf = ArcMotion.Lowered(LeafOf(name));

        if (!leaf.EndsWith(".dds", StringComparison.Ordinal))
            leaf += ".dds";

        for (int i = 0; i < built.Textures.Count; ++i)
        {
            if (built.Textures[i] == leaf && _pixels[i] is not null)
                return i;
        }

        return images.ContainsKey(leaf) ? Add(leaf) : -1;
    }

    public int Card(string sheet)
    {
        if (sheet != CaptureSheet)
            return -1;

        if (_captureAt >= 0)
            return _captureAt;

        built.Textures.Add(CaptureTexture);
        _pixels.Add(CaptureCard());
        _cutout.Add(false);
        _sheets.Add(new ArtSheet(_pixels[^1]!));
        _captureAt = built.Textures.Count - 1;

        return _captureAt;
    }

    public int Material(int material, int sheet)
    {
        if (built.Materials[material].TextureIndex == sheet)
            return material;

        if (_retextured.TryGetValue((material, sheet), out int known))
            return known;

        FbxExMaterial source = built.Materials[material];
        built.Materials.Add(new FbxExMaterial { FileName = built.Textures[sheet], TextureIndex = sheet, Value = (float[])source.Value.Clone() });
        _retextured[(material, sheet)] = built.Materials.Count - 1;

        return built.Materials.Count - 1;
    }

    public void Emit(SortedDictionary<string, byte[]> into)
    {
        if (_captureAt >= 0)
            into[CaptureTexture] = _pixels[_captureAt]!;
    }

    public bool Cutout(int index) => _cutout[index];

    public ArtSheet SheetAt(int index) => _sheets[index];

    public byte[] PixelsAt(int index) => _pixels[index] ?? [];

    public Sheeted SheetFor(EvbSprite sprite, EvbRect rect, Sheeted plate)
    {
        string name = ArcMotion.Lowered(rect.Sheet >= 0 && rect.Sheet < sprite.Sheets.Count ? sprite.Sheets[rect.Sheet] : string.Empty);
        int card = Card(name);

        if (card >= 0)
            return new Sheeted(card, new ArtSize(rect.W, rect.H), new EvbRect(rect.Sheet, 0, 0, rect.W, rect.H));

        if (name == BannerSheet)
            return plate with { Rect = new EvbRect(rect.Sheet, 0, 0, plate.Size?.Width ?? 0, BannerLines * rect.H) };

        int named = name.Length == 0 ? -1 : Find(name);

        if (named < 0)
            return plate with { Rect = rect };

        return new Sheeted(named, ArtSheet.Measure(PixelsAt(named)), rect);
    }

    private static string LeafOf(string path) => path[(path.LastIndexOfAny(['/', '\\']) + 1)..];

    private static byte[] CaptureCard()
    {
        byte[] card = new byte[128 + CardBytes];
        LittleEndian.Latin1("DDS ").CopyTo(card, 0);
        uint[] fields = [124, 0x1 | 0x2 | 0x4 | 0x1000 | 0x80000, CardSide, CardSide, CardBytes];

        for (int i = 0; i < fields.Length; ++i)
            LittleEndian.Put(card, 4 + i * 4, fields[i]);

        LittleEndian.Latin1(CaptureStamp).CopyTo(card, CaptureStampAt);
        LittleEndian.Put(card, 76, 32);
        LittleEndian.Put(card, 80, 0x4);
        LittleEndian.Latin1("DXT1").CopyTo(card, 84);
        LittleEndian.Put(card, 108, 0x1000);

        return card;
    }
}
