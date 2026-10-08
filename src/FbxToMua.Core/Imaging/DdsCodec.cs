using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Dds;

namespace FbxToMua.Core.Imaging;

public static class DdsCodec
{
    private const uint FourCcFlag = 4;
    private const uint Dxt5 = 0x35545844;
    private const uint Dxt1 = 0x31545844;
    private const int MaxSide = 8192;
    private const int BlockSide = 4;
    private const int Dxt5Stride = 16;
    private const int Dxt1Stride = 8;
    private const uint HeaderSize = 124;
    private const uint HeaderFlags = 0x1 | 0x2 | 0x4 | 0x8 | 0x1000;
    private const uint CompressedFlags = 0x1 | 0x2 | 0x4 | 0x1000 | 0x80000;
    private const int InsetShift = 16;
    private const uint PixelFormatSize = 32;
    private const uint RgbWithAlpha = 0x1 | 0x40;
    private const uint BitsPerPixel = 32;
    private const uint TextureCaps = 0x1000;
    private const int ReservedBytes = 44;
    private const int TailBytes = 16;

    private sealed class Block
    {
        public byte[][] Palette { get; } = [new byte[4], new byte[4], new byte[4], new byte[4]];
        public byte[] AlphaTable { get; } = new byte[8];
        public ulong AlphaBits { get; set; }
        public uint ColourBits { get; set; }
        public bool FourColours { get; set; }
    }

    public static BgraImage? DecodePlain(byte[] dds)
    {
        BgraImage? image = ReadSize(dds);

        if (image is null)
            return null;

        int need = image.Width * image.Height * BgraImage.Channels;

        if (dds.Length < DdsHeader.HeaderBytes + need)
            return null;

        image.Pixels = dds.AsSpan(DdsHeader.HeaderBytes, need).ToArray();
        return image;
    }

    public static BgraImage? Decode(byte[] dds)
    {
        BgraImage? image = ReadSize(dds);

        if (image is null)
            return null;

        uint flags = LittleEndian.U32(dds, 80);
        uint fourcc = LittleEndian.U32(dds, 84);

        if ((flags & FourCcFlag) == 0)
            return DecodePlain(dds);

        bool five = fourcc == Dxt5;

        if (!five && fourcc != Dxt1)
            return null;

        int blocksX = (image.Width + BlockSide - 1) / BlockSide;
        int blocksY = (image.Height + BlockSide - 1) / BlockSide;
        int stride = five ? Dxt5Stride : Dxt1Stride;

        if (dds.Length < DdsHeader.HeaderBytes + (long)blocksX * blocksY * stride)
            return null;

        image.Pixels = new byte[image.Width * image.Height * BgraImage.Channels];

        for (int by = 0; by < blocksY; ++by)
        {
            for (int bx = 0; bx < blocksX; ++bx)
            {
                int source = DdsHeader.HeaderBytes + (by * blocksX + bx) * stride;
                Block block = new();

                if (five)
                    ReadAlpha(dds, source, block);

                ReadColour(dds, five ? source + 8 : source, five, block);
                WriteBlock(block, five, bx, by, image);
            }
        }

        return image;
    }

    public static byte[] EncodeArgb(BgraImage image)
    {
        ByteSink sink = Header(image.Width, image.Height, 0, (uint)(image.Width * BgraImage.Channels));
        sink.Bytes(image.Pixels);

        return sink.ToArray();
    }

    public static byte[] EncodeDxt(BgraImage image)
    {
        bool opaque = IsOpaque(image);
        int blocksX = (image.Width + BlockSide - 1) / BlockSide;
        int blocksY = (image.Height + BlockSide - 1) / BlockSide;
        int stride = opaque ? Dxt1Stride : Dxt5Stride;
        ByteSink sink = Header(image.Width, image.Height, opaque ? Dxt1 : Dxt5, (uint)(blocksX * blocksY * stride));

        for (int by = 0; by < blocksY; ++by)
        {
            for (int bx = 0; bx < blocksX; ++bx)
            {
                byte[][] texels = Gather(image, bx, by);

                if (!opaque)
                    PackAlpha(texels, sink);

                PackColour(texels, sink);
            }
        }

        return sink.ToArray();
    }

    private static BgraImage? ReadSize(byte[] dds)
    {
        if (dds.Length < DdsHeader.HeaderBytes || !DdsHeader.IsDds(dds))
            return null;

        BgraImage image = new() { Height = (int)LittleEndian.U32(dds, 12), Width = (int)LittleEndian.U32(dds, 16) };

        return image.Width > 0 && image.Height > 0 && image.Width <= MaxSide && image.Height <= MaxSide ? image : null;
    }

    private static void Rgb565(ushort packed, byte[] colour)
    {
        colour[2] = (byte)((((packed >> 11) & 0x1f) * 255) / 31);
        colour[1] = (byte)((((packed >> 5) & 0x3f) * 255) / 63);
        colour[0] = (byte)(((packed & 0x1f) * 255) / 31);
    }

    private static void ReadAlpha(byte[] source, int at, Block block)
    {
        byte[] alpha = block.AlphaTable;
        alpha[0] = source[at];
        alpha[1] = source[at + 1];

        if (alpha[0] > alpha[1])
        {
            for (int i = 1; i < 7; ++i)
                alpha[i + 1] = (byte)(((7 - i) * alpha[0] + i * alpha[1]) / 7);
        }
        else
        {
            for (int i = 1; i < 5; ++i)
                alpha[i + 1] = (byte)(((5 - i) * alpha[0] + i * alpha[1]) / 5);

            alpha[6] = 0;
            alpha[7] = 255;
        }

        ulong bits = 0;

        for (int i = 0; i < 6; ++i)
            bits |= (ulong)source[at + 2 + i] << (8 * i);

        block.AlphaBits = bits;
    }

    private static void ReadColour(byte[] source, int at, bool five, Block block)
    {
        ushort c0 = (ushort)(source[at] | (source[at + 1] << 8));
        ushort c1 = (ushort)(source[at + 2] | (source[at + 3] << 8));

        Rgb565(c0, block.Palette[0]);
        Rgb565(c1, block.Palette[1]);
        block.FourColours = c0 > c1 || five;

        for (int i = 0; i < 3; ++i)
        {
            if (block.FourColours)
            {
                block.Palette[2][i] = (byte)((2 * block.Palette[0][i] + block.Palette[1][i]) / 3);
                block.Palette[3][i] = (byte)((block.Palette[0][i] + 2 * block.Palette[1][i]) / 3);
                continue;
            }

            block.Palette[2][i] = (byte)((block.Palette[0][i] + block.Palette[1][i]) / 2);
            block.Palette[3][i] = 0;
        }

        block.ColourBits = LittleEndian.U32(source, at + 4);
    }

    private static byte AlphaOf(Block block, bool five, int index)
    {
        if (five)
            return block.AlphaTable[(block.AlphaBits >> (3 * index)) & 7];

        bool transparent = !block.FourColours && ((block.ColourBits >> (2 * index)) & 3) == 3;

        return transparent ? (byte)0 : (byte)255;
    }

    private static void WriteBlock(Block block, bool five, int blockX, int blockY, BgraImage image)
    {
        for (int py = 0; py < BlockSide; ++py)
        {
            for (int px = 0; px < BlockSide; ++px)
            {
                int x = blockX * BlockSide + px;
                int y = blockY * BlockSide + py;

                if (x >= image.Width || y >= image.Height)
                    continue;

                int index = py * BlockSide + px;
                byte[] colour = block.Palette[(block.ColourBits >> (2 * index)) & 3];
                int target = image.At(x, y);

                image.Pixels[target] = colour[0];
                image.Pixels[target + 1] = colour[1];
                image.Pixels[target + 2] = colour[2];
                image.Pixels[target + 3] = AlphaOf(block, five, index);
            }
        }
    }

    private static ByteSink Header(int width, int height, uint fourcc, uint pitchOrSize)
    {
        bool compressed = fourcc != 0;
        ByteSink sink = new();
        sink.Text("DDS ");
        sink.Dword(HeaderSize);
        sink.Dword(compressed ? CompressedFlags : HeaderFlags);
        sink.Int(height);
        sink.Int(width);
        sink.Dword(pitchOrSize);
        sink.Dword(0);
        sink.Dword(0);
        sink.Zeros(ReservedBytes);
        sink.Dword(PixelFormatSize);
        sink.Dword(compressed ? FourCcFlag : RgbWithAlpha);
        sink.Dword(fourcc);
        sink.Dword(compressed ? 0 : BitsPerPixel);
        sink.Dword(compressed ? 0 : 0x00ff0000u);
        sink.Dword(compressed ? 0 : 0x0000ff00u);
        sink.Dword(compressed ? 0 : 0x000000ffu);
        sink.Dword(compressed ? 0 : 0xff000000u);
        sink.Dword(TextureCaps);
        sink.Zeros(TailBytes);

        return sink;
    }

    private static bool IsOpaque(BgraImage image)
    {
        for (int at = BgraImage.Alpha; at < image.Pixels.Length; at += BgraImage.Channels)
        {
            if (image.Pixels[at] != 0xff)
                return false;
        }

        return true;
    }

    private static byte[][] Gather(BgraImage image, int blockX, int blockY)
    {
        byte[][] texels = new byte[16][];

        for (int py = 0; py < BlockSide; ++py)
        {
            for (int px = 0; px < BlockSide; ++px)
            {
                int x = Math.Min(blockX * BlockSide + px, image.Width - 1);
                int y = Math.Min(blockY * BlockSide + py, image.Height - 1);
                texels[py * BlockSide + px] = image.Pixels.AsSpan(image.At(x, y), 4).ToArray();
            }
        }

        return texels;
    }

    private static void PackAlpha(byte[][] texels, ByteSink sink)
    {
        byte high = texels.Max(texel => texel[3]);
        byte low = texels.Min(texel => texel[3]);
        byte[] table = new byte[8];
        table[0] = high;
        table[1] = low;

        for (int i = 1; i < 7; ++i)
            table[i + 1] = (byte)(((7 - i) * high + i * low) / 7);

        ulong bits = 0;

        for (int i = 0; i < 16; ++i)
        {
            int best = 0;

            for (int k = 1; k < 8 && high != low; ++k)
            {
                if (Math.Abs(table[k] - texels[i][3]) < Math.Abs(table[best] - texels[i][3]))
                    best = k;
            }

            bits |= (ulong)best << (3 * i);
        }

        sink.Byte(high);
        sink.Byte(low);

        for (int i = 0; i < 6; ++i)
            sink.Byte((byte)(bits >> (8 * i)));
    }

    private static ushort To565(int[] colour)
    {
        return (ushort)(((colour[2] * 31 / 255) << 11) | ((colour[1] * 63 / 255) << 5) | (colour[0] * 31 / 255));
    }

    private static void PackColour(byte[][] texels, ByteSink sink)
    {
        int[] low = [255, 255, 255];
        int[] high = [0, 0, 0];

        foreach (byte[] texel in texels)
        {
            for (int c = 0; c < 3; ++c)
            {
                low[c] = Math.Min(low[c], texel[c]);
                high[c] = Math.Max(high[c], texel[c]);
            }
        }

        for (int c = 0; c < 3; ++c)
        {
            int inset = (high[c] - low[c]) / InsetShift;
            high[c] -= inset;
            low[c] += inset;
        }

        ushort first = To565(high);
        ushort second = To565(low);

        if (first < second)
            (first, second) = (second, first);

        byte[] packed = [(byte)first, (byte)(first >> 8), (byte)second, (byte)(second >> 8), 0, 0, 0, 0];
        Block block = new();
        ReadColour(packed, 0, true, block);
        uint bits = 0;

        for (int i = 0; i < 16 && first != second; ++i)
        {
            int best = 0;
            int bestDistance = int.MaxValue;

            for (int k = 0; k < 4; ++k)
            {
                int distance = 0;

                for (int c = 0; c < 3; ++c)
                {
                    int delta = block.Palette[k][c] - texels[i][c];
                    distance += delta * delta;
                }

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = k;
            }

            bits |= (uint)best << (2 * i);
        }

        sink.Bytes(packed.AsSpan(0, 4));

        for (int i = 0; i < 4; ++i)
            sink.Byte((byte)(bits >> (8 * i)));
    }
}
