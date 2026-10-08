using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Imaging;
using FbxToMua.Core.Import;

namespace FbxToMua.Core.Sources.Mbaa;

public sealed record MbaaTexture(string Name, byte[] Dds);

public sealed class MbaaResult
{
    public byte[] Model { get; set; } = [];
    public List<MbaaTexture> Textures { get; } = [];
    public byte[] Card { get; set; } = [];
    public List<EvbFlip> Flips { get; } = [];
    public List<EvbLamp> Lamps { get; } = [];
    public bool Fading { get; set; }
    public bool EngineClock { get; set; }
}

public static class MbaaStage
{
    public static MbaaResult? Convert(byte[] dat)
    {
        MbaaBg? file = MbaaBg.Read(dat);
        MbaaCg? cg = file is null ? null : MbaaCg.Open(dat, file.CgAt, file.CgBytes);

        if (file is null || cg is null)
            return null;

        MbaaResult result = new();

        return new MbaaConversion(file, cg, result).Run() ? result : null;
    }

    public static string Block()
    {
        string camera = "\tScale = [ 1.0, 1.0, 1.0 ],\r\n" +
            "\tPosition = [ 0.0, 0.0, 0.0 ],\r\n" +
            $"\tFOV = {MbaaGeometry.Fov.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)},\r\n" +
            "\tViewRotationX = 0.0,\r\n" +
            "\tVanishingPoint = 0.0,\r\n";

        return camera + MbaaConversion.StageBlock;
    }
}

internal sealed class MbaaConversion(MbaaBg file, MbaaCg cg, MbaaResult result)
{
    public const string StageBlock =
        "\tViewGrid = 0,\r\n\tIsFog = 0,\r\n\tFogStart = 0.0,\r\n\tFogEnd = 100.0,\r\n\tFogColor = [ 0.0, 0.0, 0.0, 0.0 ],\r\n" +
        "\tMSAA = 4,\r\n\tStageW = 4096,\r\n\tIsBloom = 0,\r\n\tShadowScale = 0.6,\r\n\tShadowAlpha = 0.7,\r\n" +
        "\tBGBloomEnable = 0,\r\n\tBGBloomBlightness = 0.8,\r\n\tBGBloomPower = 2.0,\r\n\tBGBloomBiassR = 1.0,\r\n" +
        "\tBGBloomBiassG = 1.0,\r\n\tBGBloomBiassB = 1.0,\r\n\tBGBloomBlurRadius = 1.0,\r\n\tBGBloomTextureSize = 256,\r\n" +
        "\tBGBloomAlpha = 0.0,\r\n\tBGTinyFXAAEnable = 0,\r\n\tBGTinyFXAAThreshold = 0.2,\r\n\tBGTinyFXAALerpT = 0.5,\r\n";

    private const int BlockAlign = 4;
    private const int WideSheet = 960;
    private const int CardWidth = 180;
    private const int CardHeight = 480;
    private const int CardTop = -300;
    private const uint CardBackdrop = 0xff000000u;
    private const int MostLanes = 96;
    private const int MostSheet = 4096;
    private const int LampArea = 256 * 256;
    private const int Levels = 15;
    private const float Opaque = 255.0f;

    private static readonly float[] Material = [1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 20];

    private sealed class Piece
    {
        public int Priority { get; init; }
        public int Object { get; init; }
        public int Order { get; init; }
        public FbxExNode Node { get; init; } = FbxExNode.Leaf();
        public List<float> Anime { get; set; } = [];
    }

    private sealed record StaticSheet(int Texture, int Left, int Top, int Width, int Height, int PaddedWidth, int PaddedHeight);

    private readonly record struct Canvas(int Left, int Top, int Width, int Height);

    private readonly List<Piece> _pieces = [];
    private readonly Dictionary<int, StaticSheet> _statics = [];
    private readonly Dictionary<int, int> _materials = [];
    private readonly FbxExModel _model = new();
    private MbaaTimeline _timeline = null!;
    private int _laneSheets;
    private int _order = 1 << 20;

    public bool Run()
    {
        MbaaTimeline? timeline = MbaaTimeline.Build(file);

        if (timeline is null)
            return false;

        _timeline = timeline;
        SortedDictionary<(int, int), List<int>> groups = [];

        for (int i = 0; i < _timeline.Units.Count; ++i)
        {
            MbaaUnit unit = _timeline.Units[i];

            if (IsStatic(unit, _timeline.Period))
            {
                AddStatic(unit, i);
                continue;
            }

            if (!groups.TryGetValue((unit.Object, unit.Blend), out List<int>? list))
                groups[(unit.Object, unit.Blend)] = list = [];

            list.Add(i);
        }

        AddGroups(groups);

        if (_pieces.Count == 0)
            return false;

        Card();
        Assemble();
        result.EngineClock = result.Flips.Count > 0 || result.Lamps.Count > 0;

        return true;
    }

    private static int Padded(int size)
    {
        int side = BlockAlign;

        while (side < size)
            side *= 2;

        return side;
    }

    private static bool IsStatic(MbaaUnit unit, int period)
    {
        return unit.Samples.Count == period && unit.Samples.All(sample => sample.LooksLike(unit.Samples[0]));
    }

    private static bool IsAdditive(int blend) => blend == (int)MbaaBlend.Add || blend == (int)MbaaBlend.AddStrong;

    private static int Level(int alpha) => (alpha * Levels + (int)Opaque / 2) / (int)Opaque;

    private static int Round(float value) => (int)MathF.Round(value, MidpointRounding.AwayFromZero);

    private static void PushWide(FbxExNode node, MbaaBox box, float alpha, MbaaCorners corners, bool wide)
    {
        MbaaGeometry.PushQuad(node, box, alpha, corners);

        if (!wide)
            return;

        float width = box.Right - box.Left;
        MbaaGeometry.PushQuad(node, box.Shifted(-width), alpha, corners.Mirrored());
        MbaaGeometry.PushQuad(node, box.Shifted(width), alpha, corners.Mirrored());
    }

    private static FbxExNode Leaf(int blend, int material)
    {
        FbxExNode node = FbxExNode.Leaf();
        node.BlendMode = IsAdditive(blend) ? 1 : 0;
        node.Submeshes.Add(new FbxExSubmesh { Material = material });

        return node;
    }

    private MbaaLayer LayerOf(int slot) => file.LayerOf(slot) ?? new MbaaLayer { Object = 0 };

    private int MaterialOf(int texture)
    {
        if (_materials.TryGetValue(texture, out int known))
            return known;

        _model.Materials.Add(new FbxExMaterial { FileName = result.Textures[texture].Name, TextureIndex = texture, Value = (float[])Material.Clone() });
        _materials[texture] = _model.Materials.Count - 1;

        return _model.Materials.Count - 1;
    }

    private int AddTexture(string name, BgraImage image)
    {
        result.Textures.Add(new MbaaTexture(name, DdsCodec.EncodeDxt(image)));

        return result.Textures.Count - 1;
    }

    private StaticSheet? StaticSheetOf(int image)
    {
        if (_statics.TryGetValue(image, out StaticSheet? known))
            return known;

        MbaaPicture? picture = cg.Draw(image - MbaaBg.SpriteBase);

        if (picture is null)
            return null;

        int paddedWidth = Padded(picture.Image.Width);
        int paddedHeight = Padded(picture.Image.Height);
        BgraImage padded = ImageOps.Blank(paddedWidth, paddedHeight);
        ImageOps.Copy(padded, picture.Image, 0, 0);
        ImageOps.Bleed(padded);

        int texture = AddTexture($"mbaa{image - MbaaBg.SpriteBase:D3}.dds", padded);
        StaticSheet sheet = new(texture, picture.Left, picture.Top, picture.Image.Width, picture.Image.Height, paddedWidth, paddedHeight);
        _statics[image] = sheet;

        return sheet;
    }

    private void AddStatic(MbaaUnit unit, int order)
    {
        MbaaSample look = unit.Samples[0];
        StaticSheet? sheet = StaticSheetOf(look.Image);

        if (sheet is null)
            return;

        MbaaLayer layer = LayerOf(unit.Object);
        MbaaBox box = MbaaGeometry.Place(layer.Parallax, look.X + sheet.Left, look.Y + sheet.Top, sheet.Width, sheet.Height);
        float alpha = look.Alpha / Opaque;
        Piece piece = new() { Priority = layer.Priority, Object = unit.Object, Order = order, Node = Leaf(unit.Blend, MaterialOf(sheet.Texture)) };

        PushWide(piece.Node, box, alpha, MbaaGeometry.Sheet((float)sheet.Width / sheet.PaddedWidth, (float)sheet.Height / sheet.PaddedHeight),
            sheet.Width >= WideSheet);

        result.Fading = result.Fading || alpha < 1.0f;
        _pieces.Add(piece);
    }

    private List<MbaaUnit> UnitsOf(List<int> indices) => indices.Select(index => _timeline.Units[index]).ToList();

    private void AddGroups(SortedDictionary<(int, int), List<int>> groups)
    {
        Dictionary<(int, int), int> needed = [];
        int total = 0;

        foreach (((int, int) key, List<int> indices) in groups)
        {
            int lanes = MbaaTimeline.Lanes(UnitsOf(indices), _timeline.Period, indices.Count).Count;
            needed[key] = lanes;
            total += lanes;
        }

        foreach (((int slot, int blend) key, List<int> indices) in groups)
        {
            int wanted = needed[key];
            int cap = total <= MostLanes ? wanted : Math.Max(1, wanted * MostLanes / total);
            AddGroup(key.slot, key.blend, UnitsOf(indices), cap);
        }
    }

    private static bool Fixed(List<MbaaUnit> units)
    {
        Dictionary<int, (float X, float Y)> placed = [];

        foreach (MbaaSample sample in units.SelectMany(unit => unit.Samples))
        {
            if (!placed.TryAdd(sample.Image, (sample.X, sample.Y)) && placed[sample.Image] != (sample.X, sample.Y))
                return false;
        }

        return true;
    }

    private static Canvas CanvasOf(List<MbaaUnit> units, Dictionary<int, MbaaPicture> pictures)
    {
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;

        foreach (MbaaSample sample in units.SelectMany(unit => unit.Samples))
        {
            MbaaPicture picture = pictures[sample.Image];
            int x = Round(sample.X) + picture.Left;
            int y = Round(sample.Y) + picture.Top;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x + picture.Image.Width);
            bottom = Math.Max(bottom, y + picture.Image.Height);
        }

        return new Canvas(left, top, right - left, bottom - top);
    }

    private Dictionary<int, MbaaPicture>? Draw(List<MbaaUnit> units)
    {
        Dictionary<int, MbaaPicture> pictures = [];

        foreach (MbaaSample sample in units.SelectMany(unit => unit.Samples))
        {
            if (pictures.ContainsKey(sample.Image))
                continue;

            MbaaPicture? picture = cg.Draw(sample.Image - MbaaBg.SpriteBase);

            if (picture is null)
                return null;

            pictures[sample.Image] = picture;
        }

        return pictures.Count > 0 ? pictures : null;
    }

    private List<int> AlphaOf(List<MbaaUnit> lane)
    {
        int[] alpha = Enumerable.Repeat(-1, _timeline.Period).ToArray();

        foreach (MbaaSample sample in lane.SelectMany(unit => unit.Samples))
            alpha[sample.Tick] = sample.Alpha;

        return [.. alpha];
    }

    private List<EvbLamp>? Lamps(List<List<MbaaUnit>> lanes)
    {
        if (result.Lamps.Count + lanes.Count > ImMarks.LampSlots)
            return null;

        List<EvbLamp> lamps = [];

        foreach (List<MbaaUnit> lane in lanes)
        {
            List<EvbRamp>? ramps = MbaaTimeline.Ramps(AlphaOf(lane));

            if (ramps is null)
                return null;

            EvbLamp lamp = new() { Loop = _timeline.Period };
            lamp.Ramp.AddRange(ramps);
            lamps.Add(lamp);
        }

        return lamps;
    }

    private static BgraImage CellImage(MbaaPicture picture, MbaaSample look, int level, bool isFixed, Canvas canvas)
    {
        BgraImage cell = picture.Image.Copy();

        if (isFixed)
        {
            cell = ImageOps.Blank(canvas.Width, canvas.Height);
            ImageOps.Copy(cell, picture.Image, Round(look.X) + picture.Left - canvas.Left, Round(look.Y) + picture.Top - canvas.Top);
        }

        if (level < Levels)
            ImageOps.Faded(cell, (float)level / Levels);

        return cell;
    }

    private int Slot(EvbFlip flip)
    {
        for (int i = 0; i < result.Flips.Count; ++i)
        {
            if (result.Flips[i].Rects.SequenceEqual(flip.Rects) && result.Flips[i].Frame.SequenceEqual(flip.Frame))
                return i;
        }

        if (result.Flips.Count >= ImMarks.FlipSlots)
            return -1;

        result.Flips.Add(flip);
        return result.Flips.Count - 1;
    }

    private void AddGroup(int slot, int blend, List<MbaaUnit> units, int cap)
    {
        List<List<int>> laneIndices = MbaaTimeline.Lanes(units, _timeline.Period, cap);
        List<List<MbaaUnit>> lanes = laneIndices.Select(indices => indices.Select(index => units[index]).ToList()).ToList();
        List<MbaaUnit> kept = lanes.SelectMany(lane => lane).ToList();
        Dictionary<int, MbaaPicture>? pictures = lanes.Count == 0 ? null : Draw(kept);

        if (pictures is null)
            return;

        MbaaLayer layer = LayerOf(slot);
        bool isFixed = Fixed(kept);
        Canvas canvas = isFixed ? CanvasOf(kept, pictures) : default;
        int firstAlpha = kept[0].Samples[0].Alpha;
        bool constant = kept.All(unit => unit.Samples.All(sample => sample.Alpha == firstAlpha));
        List<EvbLamp>? lamps = isFixed && !constant && canvas.Width * canvas.Height >= LampArea ? Lamps(lanes) : null;
        bool lit = lamps is not null;
        bool baked = !constant && !lit;

        Dictionary<(int Image, int Level), int> cells = [];
        List<((int Image, int Level) Key, MbaaSample Sample)> looks = [];

        foreach (MbaaSample sample in kept.SelectMany(unit => unit.Samples))
        {
            (int, int) key = (sample.Image, baked ? Level(sample.Alpha) : Levels);

            if (key.Item2 == 0 || cells.ContainsKey(key))
                continue;

            cells[key] = looks.Count;
            looks.Add((key, sample));
        }

        List<CellSize> sizes = looks.Select(look => isFixed ? new CellSize(canvas.Width, canvas.Height)
            : new CellSize(pictures[look.Key.Image].Image.Width, pictures[look.Key.Image].Image.Height)).ToList();
        AtlasLayout? layout = MbaaAtlas.Pack(sizes, MostSheet);

        if (layout is null)
            return;

        BgraImage sheet = ImageOps.Blank(layout.Width, layout.Height);
        List<float> rects = [];

        for (int i = 0; i < looks.Count; ++i)
        {
            PixelRect rect = layout.Rects[i];
            BgraImage cell = CellImage(pictures[looks[i].Key.Image], looks[i].Sample, looks[i].Key.Level, isFixed, canvas);

            if (cell.Width != rect.Width || cell.Height != rect.Height)
                cell = ImageOps.Resized(cell, rect.Width, rect.Height);

            ImageOps.Bleed(cell);
            ImageOps.Copy(sheet, cell, rect.X, rect.Y);
            rects.AddRange([(float)rect.X / layout.Width, (float)rect.Width / layout.Width, (float)rect.Y / layout.Height, (float)rect.Height / layout.Height]);
        }

        int material = MaterialOf(AddTexture($"mbaa_lane{_laneSheets++:D2}.dds", sheet));
        float vertexAlpha = constant ? firstAlpha / Opaque : 1.0f;

        for (int l = 0; l < lanes.Count; ++l)
            AddLane(layer, slot, blend, lanes[l], cells, baked, isFixed, canvas, rects, material, vertexAlpha, lit ? lamps![l] : null, pictures);
    }

    private void AddLane(MbaaLayer layer, int slot, int blend, List<MbaaUnit> lane, Dictionary<(int, int), int> cells, bool baked, bool isFixed,
        Canvas canvas, List<float> rects, int material, float vertexAlpha, EvbLamp? lamp, Dictionary<int, MbaaPicture> pictures)
    {
        EvbFlip flip = new();
        flip.Rects.AddRange(rects);
        flip.Frame.AddRange(Enumerable.Repeat(-1, _timeline.Period));
        List<float>[] matrices = Enumerable.Range(0, _timeline.Period).Select(_ => new List<float>()).ToArray();

        foreach (MbaaSample sample in lane.SelectMany(unit => unit.Samples))
        {
            if (!cells.TryGetValue((sample.Image, baked ? Level(sample.Alpha) : Levels), out int cell))
                continue;

            flip.Frame[sample.Tick] = cell;

            if (isFixed)
                continue;

            MbaaPicture picture = pictures[sample.Image];
            matrices[sample.Tick] = MbaaGeometry.Matrix(MbaaGeometry.Place(layer.Parallax, sample.X + picture.Left, sample.Y + picture.Top,
                picture.Image.Width, picture.Image.Height));
        }

        int flipSlot = Slot(flip);

        if (flipSlot < 0)
            return;

        int lampSlot = -1;

        if (lamp is not null)
        {
            lampSlot = result.Lamps.Count;
            result.Lamps.Add(lamp);
        }

        Piece piece = new() { Priority = layer.Priority, Object = slot, Order = _order++, Node = Leaf(blend, material) };
        MbaaCorners corners = MbaaGeometry.Flip(flipSlot, lampSlot);

        if (isFixed)
        {
            MbaaBox box = MbaaGeometry.Place(layer.Parallax, canvas.Left, canvas.Top, canvas.Width, canvas.Height);
            PushWide(piece.Node, box, vertexAlpha, corners, canvas.Width >= WideSheet);
        }
        else
        {
            MbaaGeometry.PushQuad(piece.Node, MbaaGeometry.Unit(), vertexAlpha, corners);
            piece.Anime = Filled(matrices);
        }

        result.Fading = result.Fading || vertexAlpha < 1.0f;
        _pieces.Add(piece);
    }

    private static List<float> Filled(List<float>[] matrices)
    {
        int first = Array.FindIndex(matrices, matrix => matrix.Count > 0);

        if (first < 0)
            return [];

        List<float> filled = [];
        List<float> held = matrices[first];

        foreach (List<float> matrix in matrices)
        {
            if (matrix.Count > 0)
                held = matrix;

            filled.AddRange(held);
        }

        return filled;
    }

    private void Card()
    {
        BgraImage card = ImageOps.Blank(CardWidth, CardHeight, CardBackdrop);
        var shown = _timeline.Units
            .Where(unit => unit.Samples.Count > 0 && unit.Samples[0].Tick == 0)
            .Select(unit => (Priority: LayerOf(unit.Object).Priority, unit.Object, Unit: unit))
            .OrderBy(entry => entry.Priority).ThenBy(entry => entry.Object)
            .ToList();

        foreach ((int _, int _, MbaaUnit unit) in shown)
        {
            MbaaSample sample = unit.Samples[0];
            MbaaPicture? picture = cg.Draw(sample.Image - MbaaBg.SpriteBase);

            if (picture is not null)
                PaintCard(card, picture, sample.X, sample.Y, unit.Blend, sample.Alpha);
        }

        result.Card = DdsCodec.EncodeArgb(card);
    }

    private static void PaintCard(BgraImage card, MbaaPicture picture, float x, float y, int blend, int alpha)
    {
        int left = Round(x) + picture.Left - MbaaGeometry.OriginX + CardWidth / 2;
        int top = Round(y) + picture.Top - CardTop;
        float opacity = alpha / Opaque;

        if (IsAdditive(blend))
        {
            ImageOps.Add(card, picture.Image, left, top, opacity);
            return;
        }

        BgraImage faded = picture.Image.Copy();

        if (opacity < 1.0f)
            ImageOps.Faded(faded, opacity);

        ImageOps.Over(card, faded, left, top);
    }

    private void Assemble()
    {
        List<Piece> ordered = _pieces.OrderBy(piece => piece.Priority).ThenBy(piece => piece.Object).ThenBy(piece => piece.Order).ToList();
        _model.Textures = result.Textures.Select(texture => texture.Name).ToList();

        FbxExNode root = FbxExNode.Branch();
        root.Child = 1;
        _model.Nodes.Add(root);
        _model.Animes.Add(MbaaGeometry.Rest());

        for (int i = 0; i < ordered.Count; ++i)
        {
            Piece piece = ordered[i];
            piece.Node.Sibling = i + 1 < ordered.Count ? i + 2 : -1;
            _model.Nodes.Add(piece.Node);
            _model.Animes.Add(piece.Anime.Count == 0 ? MbaaGeometry.Rest() : piece.Anime);
        }

        result.Model = FbxExWriter.Build(_model);
    }
}
