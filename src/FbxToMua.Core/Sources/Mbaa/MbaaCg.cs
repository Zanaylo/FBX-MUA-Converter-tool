using FbxToMua.Core.Binary;
using FbxToMua.Core.Imaging;

namespace FbxToMua.Core.Sources.Mbaa;

internal sealed record MbaaPicture(BgraImage Image, int Left, int Top);

internal sealed class MbaaCg
{
    private const string Magic = "BMP Cutter3";
    private const int PaletteAt = 0x14;
    private const int Palettes = 8;
    private const int PaletteEntries = 256;
    private const int HeaderAt = PaletteAt + Palettes * PaletteEntries * 4;
    private const int IndicesAt = HeaderAt + 12 * 4;
    private const uint ImageSlots = 3000;
    private const int ImageNameBytes = 32;
    private const int ImageHeader = ImageNameBytes + 10 * 4;
    private const int AlignmentBytes = 24;
    private const int Cell = 16;
    private const int CellsPerPage = 256;
    private const int PageSide = 16;
    private const int PaletteBytes = 1024;
    private const int ColourKeyBytes = 4;
    private const int BytesPerPixel = 4;
    private const uint OpaqueBits = 0xff000000u;
    private const int MostSide = 8192;

    private const int TypeSkip = -1;
    private const int TypeDirect = 1;
    private const int TypeOwnPalette = 2;
    private const int TypeColourKey = 3;
    private const int TypeIndexedAlpha = 4;

    private sealed class ImageEntry
    {
        public int Type { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public int Bpp { get; init; }
        public int[] Bounds { get; init; } = new int[4];
        public uint AlignStart { get; init; }
        public uint AlignCount { get; init; }
        public long Pixels { get; init; }
    }

    private readonly record struct Alignment(int X, int Y, int Width, int Height, short SourceX, short SourceY, short SourceImage, short Copy);

    private struct CellRef
    {
        public long Start;
        public int Width;
        public long Offset;
        public int Type;
    }

    private readonly byte[] _data;
    private readonly int _base;
    private readonly int _size;
    private readonly uint _count;
    private readonly uint _alignCount;
    private readonly uint _pageCount;
    private readonly long _alignments;
    private readonly uint[] _palette = new uint[PaletteEntries];
    private CellRef[] _cells = [];

    private MbaaCg(byte[] data, int at, int size)
    {
        _data = data;
        _base = at;
        _size = size;
        _pageCount = Dword(HeaderAt) + 1;
        _alignCount = Dword(HeaderAt + 8);
        _count = Dword(HeaderAt + 12);
        _alignments = Dword(IndicesAt + ImageSlots * 4);
    }

    public static MbaaCg? Open(byte[] data, int at, int size)
    {
        if (size < IndicesAt + (ImageSlots + 1) * 4 || !LittleEndian.Starts(data.AsSpan(at), Magic))
            return null;

        MbaaCg cg = new(data, at, size);

        if (cg._count >= ImageSlots || cg._alignments + (long)cg._alignCount * AlignmentBytes > size)
            return null;

        for (int i = 0; i < PaletteEntries; ++i)
            cg._palette[i] = cg.Dword(PaletteAt + i * 4) | OpaqueBits;

        cg._palette[0] = 0;
        cg.BuildCells();

        return cg;
    }

    public MbaaPicture? Draw(int index)
    {
        ImageEntry? image = ReadImage(index);

        if (image is null)
            return null;

        int left = image.Bounds[0];
        int top = image.Bounds[1];
        int width = image.Bounds[2] - left + 1;
        int height = image.Bounds[3] - top + 1;

        if (width <= 0 || height <= 0 || width > MostSide || height > MostSide)
            return null;

        uint[] palette = PaletteFor(image);
        BgraImage picture = new() { Width = width, Height = height, Pixels = new byte[width * height * BytesPerPixel] };

        for (uint i = 0; i < image.AlignCount; ++i)
        {
            Alignment? alignment = ReadAlignment(image.AlignStart + i);

            if (alignment is not null)
                Paint(alignment.Value, palette, left, top, picture);
        }

        return new MbaaPicture(picture, left, top);
    }

    private uint Dword(long at) => LittleEndian.U32(_data, _base + at);

    private int Int(long at) => (int)Dword(at);

    private short Short(long at) => (short)LittleEndian.U16(_data, _base + at);

    private byte ByteAt(long at) => _data[_base + at];

    private static bool CarriesPalette(int type) => type == TypeOwnPalette || type == TypeIndexedAlpha;

    private static int RowBytes(int type) => type == TypeDirect ? BytesPerPixel : 1;

    private static int PlaneMultiplier(int type) => type == TypeDirect ? BytesPerPixel : type == TypeIndexedAlpha ? 2 : 1;

    private uint[] PaletteFor(ImageEntry image)
    {
        if (image.Bpp == 32 && image.Type == TypeColourKey && image.Pixels + 4 <= _size)
        {
            uint colour = Dword(image.Pixels) & 0x00ffffffu;
            uint[] keyed = new uint[PaletteEntries];

            for (uint i = 1; i < PaletteEntries; ++i)
                keyed[i] = (i << 24) | colour;

            return keyed;
        }

        if (image.Bpp == 32 && CarriesPalette(image.Type) && image.Pixels + PaletteBytes <= _size)
            return Enumerable.Range(0, PaletteEntries).Select(i => Dword(image.Pixels + i * 4) | OpaqueBits).ToArray();

        return _palette;
    }

    private ImageEntry? ReadImage(int index)
    {
        if (index < 0 || (uint)index >= _count)
            return null;

        long at = Dword(IndicesAt + index * 4L);

        if (at == 0 || at + ImageHeader > _size)
            return null;

        long fields = at + ImageNameBytes;
        ImageEntry image = new()
        {
            Type = Int(fields),
            Width = Int(fields + 4),
            Height = Int(fields + 8),
            Bpp = Int(fields + 12),
            Bounds = [Int(fields + 16), Int(fields + 20), Int(fields + 24), Int(fields + 28)],
            AlignStart = Dword(fields + 32),
            AlignCount = Dword(fields + 36),
            Pixels = at + ImageHeader,
        };

        return image.Type != TypeSkip && (long)image.AlignStart + image.AlignCount <= _alignCount ? image : null;
    }

    private Alignment? ReadAlignment(uint index)
    {
        long at = _alignments + (long)index * AlignmentBytes;

        if (index >= _alignCount || at + AlignmentBytes > _size)
            return null;

        return new Alignment(Int(at), Int(at + 4), Int(at + 8), Int(at + 12), Short(at + 16), Short(at + 18), Short(at + 20), Short(at + 22));
    }

    private void BuildCells()
    {
        _cells = new CellRef[_pageCount * CellsPerPage];

        for (uint index = 0; index < _count; ++index)
        {
            ImageEntry? image = ReadImage((int)index);

            if (image is null)
                continue;

            long address = image.Pixels;

            if (image.Bpp == 32 && image.Type == TypeColourKey)
                address += ColourKeyBytes;
            else if (image.Bpp == 32 && CarriesPalette(image.Type))
                address += PaletteBytes;

            for (uint i = 0; i < image.AlignCount; ++i)
            {
                Alignment? found = ReadAlignment(image.AlignStart + i);

                if (found is null || found.Value.Copy != 0)
                    continue;

                Alignment alignment = found.Value;
                int column = alignment.SourceX / Cell;
                int row = alignment.SourceY / Cell;
                int across = column + alignment.Width / Cell >= PageSide ? PageSide - column : alignment.Width / Cell;
                int down = row + alignment.Height / Cell >= PageSide ? PageSide - row : alignment.Height / Cell;

                if (alignment.SourceImage < 0 || (uint)alignment.SourceImage >= _pageCount)
                    continue;

                long page = (long)alignment.SourceImage * CellsPerPage;

                for (int a = 0; a < down; ++a)
                {
                    for (int b = 0; b < across; ++b)
                    {
                        _cells[page + (row + a) * PageSide + column + b] = new CellRef
                        {
                            Start = address,
                            Width = alignment.Width,
                            Offset = (long)(b * Cell + a * alignment.Width * Cell) * RowBytes(image.Type),
                            Type = image.Type,
                        };
                    }
                }

                address += (long)alignment.Width * alignment.Height * PlaneMultiplier(image.Type);
            }
        }
    }

    private void Paint(Alignment alignment, uint[] palette, int left, int top, BgraImage picture)
    {
        if (alignment.SourceImage < 0 || (uint)alignment.SourceImage >= _pageCount)
            return;

        long page = (long)alignment.SourceImage * CellsPerPage;
        int first = alignment.SourceY / Cell * PageSide + alignment.SourceX / Cell;

        for (int a = 0; a < alignment.Height / Cell; ++a)
        {
            for (int b = 0; b < alignment.Width / Cell; ++b)
            {
                int slot = first + a * PageSide + b;

                if (slot < 0 || slot >= CellsPerPage)
                    continue;

                CellRef cell = _cells[page + slot];
                long rowBytes = (long)cell.Width * RowBytes(cell.Type);

                if (cell.Start == 0 || cell.Start + cell.Offset + rowBytes * (Cell - 1) + Cell * RowBytes(cell.Type) > _size)
                    continue;

                PaintCell(alignment, palette, left, top, picture, a, b, cell, rowBytes);
            }
        }
    }

    private void PaintCell(Alignment alignment, uint[] palette, int left, int top, BgraImage picture, int a, int b, CellRef cell, long rowBytes)
    {
        long source = cell.Start + cell.Offset;
        long alphaPlane = (long)alignment.Width * alignment.Height;

        for (int r = 0; r < Cell; ++r)
        {
            int y = alignment.Y + a * Cell + r - top;

            if (y < 0 || y >= picture.Height)
                continue;

            for (int c = 0; c < Cell; ++c)
            {
                int x = alignment.X + b * Cell + c - left;

                if (x < 0 || x >= picture.Width)
                    continue;

                uint pixel = cell.Type == TypeDirect ? Dword(source + r * rowBytes + c * BytesPerPixel) : palette[ByteAt(source + r * rowBytes + c)];

                if (cell.Type == TypeIndexedAlpha && source + alphaPlane + r * rowBytes + c < _size)
                    pixel = (pixel & 0x00ffffffu) | ((uint)ByteAt(source + alphaPlane + r * rowBytes + c) << 24);

                BitConverter.TryWriteBytes(picture.Pixels.AsSpan(picture.At(x, y)), pixel);
            }
        }
    }
}
