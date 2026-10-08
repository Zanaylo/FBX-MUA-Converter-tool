using FbxToMua.Core.Imaging;

namespace FbxToMua.Core.Sources.Mbaa;

internal readonly record struct CellSize(int Width, int Height);

internal sealed class AtlasLayout
{
    public int Width { get; set; }
    public int Height { get; set; }
    public float Scale { get; set; } = 1.0f;
    public PixelRect[] Rects { get; set; } = [];
}

internal static class MbaaAtlas
{
    private const int Gap = 4;
    private const int Smallest = 4;
    private const int Shrinks = 5;

    public static AtlasLayout? Pack(IReadOnlyList<CellSize> cells, int most)
    {
        if (cells.Count == 0)
            return null;

        float scale = 1.0f;

        for (int shrink = 0; shrink <= Shrinks; ++shrink, scale *= 0.5f)
        {
            float current = scale;
            List<CellSize> sized = cells.Select(cell => new CellSize(Math.Max(1, (int)(cell.Width * current)), Math.Max(1, (int)(cell.Height * current)))).ToList();
            AtlasLayout? layout = PackAt(sized, most);

            if (layout is null)
                continue;

            layout.Scale = scale;
            return layout;
        }

        return null;
    }

    private static int Aligned(int value) => (value + Gap - 1) / Gap * Gap;

    private static int PowerOfTwo(int value)
    {
        int side = Smallest;

        while (side < value)
            side *= 2;

        return side;
    }

    private static AtlasLayout? Shelves(List<CellSize> sizes, List<int> order, int width, int most)
    {
        PixelRect[] rects = new PixelRect[sizes.Count];
        int x = 0;
        int y = 0;
        int shelf = 0;

        foreach (int index in order)
        {
            CellSize size = sizes[index];

            if (size.Width > width)
                return null;

            if (x + size.Width > width)
            {
                x = 0;
                y += Aligned(shelf) + Gap;
                shelf = 0;
            }

            rects[index] = new PixelRect(x, y, size.Width, size.Height);
            x += Aligned(size.Width) + Gap;
            shelf = Math.Max(shelf, size.Height);
        }

        AtlasLayout layout = new() { Width = width, Height = PowerOfTwo(y + shelf), Rects = rects };

        return layout.Height <= most ? layout : null;
    }

    private static AtlasLayout? PackAt(List<CellSize> sizes, int most)
    {
        List<int> order = Enumerable.Range(0, sizes.Count).OrderByDescending(index => sizes[index].Height).ToList();
        int widest = sizes.Max(size => size.Width);
        AtlasLayout? best = null;

        for (int width = PowerOfTwo(widest); width <= most; width *= 2)
        {
            AtlasLayout? tried = Shelves(sizes, order, width, most);

            if (tried is null)
                continue;

            if (best is not null && (long)tried.Width * tried.Height >= (long)best.Width * best.Height)
                continue;

            best = tried;
        }

        return best;
    }
}
