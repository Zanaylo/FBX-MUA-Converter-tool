using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Import;

public readonly record struct ArtSize(int Width, int Height);

public sealed class ArtSheet
{
    private const int Header = 128;
    private const int Least = 148;
    private const int Looked = 4096;
    private const int Sampled = 4096;
    private const double CardBlack = 8.0 / 255.0;

    private readonly byte[] _blob = [];
    private readonly int _step;
    private readonly int _colour;
    private readonly bool _explicit;
    private readonly int _wide = 1;
    private readonly int _high = 1;
    private readonly bool _readable;

    public bool Lit { get; }
    public bool Dark { get; }
    public bool Hidden { get; }

    public ArtSheet(byte[] blob)
    {
        if (!TryBlocks(blob, out int step, out _))
            return;

        _blob = blob;
        _readable = true;
        _step = step;
        _colour = step == 8 ? 0 : 8;
        _explicit = FourCc(blob, "DXT3");

        if (Measure(blob) is ArtSize size)
        {
            _wide = (size.Width + 3) / 4;
            _high = (size.Height + 3) / 4;
        }

        int count = (blob.Length - Header) / step;
        int stride = count < Sampled ? 1 : count / Sampled;
        double peak = 0.0;
        bool clear = false;
        bool hidden = count > 0;

        for (int block = 0; block < count; block += stride)
        {
            int at = Header + block * step;
            hidden = hidden && Vanishes(blob, at, step, _explicit);
            clear = clear || (step == 8 && Punched(blob, at)) || (step == 16 && LeastAlpha(blob, at, _explicit) < 255);
            peak = Math.Max(peak, TexelPeak(blob, at + _colour, step == 8));
        }

        Lit = !clear && peak > CardBlack;
        Dark = !clear && peak <= CardBlack;
        Hidden = hidden;
    }

    public static ArtSize? Measure(byte[] blob)
    {
        if (!IsDds(blob))
            return null;

        ArtSize size = new((int)LittleEndian.U32(blob, 16), (int)LittleEndian.U32(blob, 12));

        return size.Width > 0 && size.Height > 0 ? size : null;
    }

    public static bool Transparent(byte[] blob)
    {
        if (!TryBlocks(blob, out int step, out int alpha))
            return false;

        int count = (blob.Length - Header) / step;
        int looked = Math.Min(count, Looked);

        if (FourCc(blob, "DXT1"))
        {
            for (int i = 0; i < looked; ++i)
            {
                int at = Header + i * step;

                if (LittleEndian.U16(blob, at) > LittleEndian.U16(blob, at + 2))
                    continue;

                uint indices = LittleEndian.U32(blob, at + 4);

                for (int t = 0; t < 16; ++t)
                {
                    if (((indices >> (t * 2)) & 3) == 3)
                        return true;
                }
            }

            return false;
        }

        for (int i = 0; i < looked; ++i)
        {
            int at = Header + i * step;

            if (alpha == 0)
            {
                if (blob[at] < 250 && blob[at + 1] < 250)
                    return true;

                continue;
            }

            for (int k = 0; k < 8; ++k)
            {
                if (blob[at + k] != 0xff)
                    return true;
            }
        }

        return false;
    }

    public double Peak(double u, double w)
    {
        if (!_readable)
            return 1.0;

        long at = Header + ((long)Wrapped(w, _high) * _wide + Wrapped(u, _wide)) * _step + _colour;

        return at + 8 <= _blob.Length ? TexelPeak(_blob, (int)at, _step == 8) : 1.0;
    }

    private static bool IsDds(byte[] blob) => blob.Length >= Least && LittleEndian.Starts(blob, "DDS ");

    private static bool FourCc(byte[] blob, string code) => LittleEndian.TagAt(blob, 84, code);

    private static bool TryBlocks(byte[] blob, out int step, out int alpha)
    {
        step = 0;
        alpha = -1;

        if (!IsDds(blob))
            return false;

        if (FourCc(blob, "DXT1"))
            step = 8;
        else if (FourCc(blob, "DXT5"))
            (step, alpha) = (16, 0);
        else if (FourCc(blob, "DXT3"))
            step = 16;
        else
            return false;

        return blob.Length - Header >= step;
    }

    private static int[] AlphaLevels(int first, int second)
    {
        int[] levels = new int[8];
        levels[0] = first;
        levels[1] = second;

        if (first > second)
        {
            for (int k = 0; k < 6; ++k)
                levels[2 + k] = ((6 - k) * first + (1 + k) * second) / 7;

            return levels;
        }

        for (int k = 0; k < 4; ++k)
            levels[2 + k] = ((4 - k) * first + (1 + k) * second) / 5;

        levels[6] = 0;
        levels[7] = 255;

        return levels;
    }

    private static ulong SixBytes(byte[] blob, int at)
    {
        ulong bits = 0;

        for (int i = 0; i < 6; ++i)
            bits |= (ulong)blob[at + i] << (8 * i);

        return bits;
    }

    private static int LeastAlpha(byte[] blob, int at, bool explicitAlpha)
    {
        int least = 255;

        if (explicitAlpha)
        {
            for (int k = 0; k < 16; ++k)
                least = Math.Min(least, ((blob[at + k / 2] >> (k % 2 * 4)) & 15) * 17);

            return least;
        }

        int[] levels = AlphaLevels(blob[at], blob[at + 1]);
        ulong bits = SixBytes(blob, at + 2);

        for (int k = 0; k < 16; ++k)
            least = Math.Min(least, levels[(bits >> (k * 3)) & 7]);

        return least;
    }

    private static bool Vanishes(byte[] blob, int at, int step, bool explicitAlpha)
    {
        if (step == 8)
            return LittleEndian.U16(blob, at) <= LittleEndian.U16(blob, at + 2) && LittleEndian.U32(blob, at + 4) == 0xffffffffu;

        if (explicitAlpha)
            return blob.AsSpan(at, 8).IndexOfAnyExcept((byte)0) < 0;

        int[] levels = AlphaLevels(blob[at], blob[at + 1]);
        ulong bits = SixBytes(blob, at + 2);

        for (int k = 0; k < 16; ++k)
        {
            if (levels[(bits >> (k * 3)) & 7] != 0)
                return false;
        }

        return true;
    }

    private static bool Punched(byte[] blob, int at)
    {
        if (LittleEndian.U16(blob, at) > LittleEndian.U16(blob, at + 2))
            return false;

        uint indices = LittleEndian.U32(blob, at + 4);

        for (int k = 0; k < 16; ++k)
        {
            if (((indices >> (k * 2)) & 3) == 3)
                return true;
        }

        return false;
    }

    private static double TexelPeak(byte[] blob, int at, bool punchable)
    {
        ushort first = LittleEndian.U16(blob, at);
        ushort second = LittleEndian.U16(blob, at + 2);
        uint indices = LittleEndian.U32(blob, at + 4);
        double[][] palette = [new double[3], new double[3], new double[3], new double[3]];

        for (int e = 0; e < 2; ++e)
        {
            ushort value = e == 0 ? first : second;
            palette[e][0] = ((value >> 11) & 31) / 31.0;
            palette[e][1] = ((value >> 5) & 63) / 63.0;
            palette[e][2] = (value & 31) / 31.0;
        }

        for (int k = 0; k < 3; ++k)
        {
            bool four = first > second || !punchable;
            palette[2][k] = four ? (2 * palette[0][k] + palette[1][k]) / 3.0 : (palette[0][k] + palette[1][k]) / 2.0;
            palette[3][k] = four ? (palette[0][k] + 2 * palette[1][k]) / 3.0 : 0.0;
        }

        double peak = 0.0;

        for (int t = 0; t < 16; ++t)
        {
            double[] texel = palette[(indices >> (t * 2)) & 3];

            for (int k = 0; k < 3; ++k)
                peak = texel[k] > peak ? texel[k] : peak;
        }

        return peak;
    }

    private static int Wrapped(double value, int span)
    {
        int at = (int)Math.Floor(value * span) % span;

        return at < 0 ? at + span : at;
    }
}
