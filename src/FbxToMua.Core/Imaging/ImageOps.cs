namespace FbxToMua.Core.Imaging;

public static class ImageOps
{
    private const int Channels = BgraImage.Channels;
    private const int Alpha = BgraImage.Alpha;
    private const float Opaque = 255.0f;
    private const byte Open = 0;
    private const byte Queued = 1;
    private const byte Filled = 2;
    private const int BleedPasses = 16;

    private static readonly (int X, int Y)[] Steps = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    private readonly record struct Tap(int Index, float Weight);

    public static BgraImage Blank(int width, int height, uint bgra = 0)
    {
        BgraImage image = new() { Width = width, Height = height, Pixels = new byte[width * height * Channels] };

        for (int at = 0; at < image.Pixels.Length; at += Channels)
        {
            for (int c = 0; c < Channels; ++c)
                image.Pixels[at + c] = (byte)(bgra >> (8 * c));
        }

        return image;
    }

    public static BgraImage Crop(BgraImage source, PixelRect area)
    {
        BgraImage cropped = Blank(area.Width, area.Height);

        for (int y = 0; y < area.Height; ++y)
        {
            for (int x = 0; x < area.Width; ++x)
            {
                if (source.Inside(area.X + x, area.Y + y))
                    Array.Copy(source.Pixels, source.At(area.X + x, area.Y + y), cropped.Pixels, cropped.At(x, y), Channels);
            }
        }

        return cropped;
    }

    public static BgraImage Resized(BgraImage source, int width, int height)
    {
        if (width <= 0 || height <= 0 || source.Width <= 0 || source.Height <= 0)
            return Blank(Math.Max(width, 0), Math.Max(height, 0));

        List<Tap>[] across = TapsFor(source.Width, width);
        List<Tap>[] down = TapsFor(source.Height, height);
        float[] premultiplied = Premultiplied(source);
        float[] rows = new float[width * source.Height * Channels];

        for (int y = 0; y < source.Height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                int sum = (y * width + x) * Channels;

                foreach (Tap tap in across[x])
                {
                    int input = (y * source.Width + tap.Index) * Channels;

                    for (int c = 0; c < Channels; ++c)
                        rows[sum + c] += premultiplied[input + c] * tap.Weight;
                }
            }
        }

        BgraImage resized = Blank(width, height);

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                float[] sum = new float[Channels];

                foreach (Tap tap in down[y])
                {
                    int input = (tap.Index * width + x) * Channels;

                    for (int c = 0; c < Channels; ++c)
                        sum[c] += rows[input + c] * tap.Weight;
                }

                Store(sum, resized.Pixels, resized.At(x, y));
            }
        }

        return resized;
    }

    public static void Copy(BgraImage target, BgraImage source, int x, int y)
    {
        for (int row = 0; row < source.Height; ++row)
        {
            for (int column = 0; column < source.Width; ++column)
            {
                if (target.Inside(x + column, y + row))
                    Array.Copy(source.Pixels, source.At(column, row), target.Pixels, target.At(x + column, y + row), Channels);
            }
        }
    }

    public static void Over(BgraImage target, BgraImage source, int x, int y)
    {
        for (int row = 0; row < source.Height; ++row)
        {
            for (int column = 0; column < source.Width; ++column)
            {
                if (!target.Inside(x + column, y + row))
                    continue;

                int top = source.At(column, row);
                int under = target.At(x + column, y + row);
                float topAlpha = source.Pixels[top + Alpha] / Opaque;
                float underAlpha = target.Pixels[under + Alpha] / Opaque;
                float alpha = topAlpha + underAlpha * (1.0f - topAlpha);

                if (alpha <= 0.0f)
                    continue;

                for (int c = 0; c < Alpha; ++c)
                {
                    target.Pixels[under + c] = Byte((source.Pixels[top + c] * topAlpha
                        + target.Pixels[under + c] * underAlpha * (1.0f - topAlpha)) / alpha);
                }

                target.Pixels[under + Alpha] = Byte(alpha * Opaque);
            }
        }
    }

    public static void Add(BgraImage target, BgraImage source, int x, int y, float strength)
    {
        for (int row = 0; row < source.Height; ++row)
        {
            for (int column = 0; column < source.Width; ++column)
            {
                if (!target.Inside(x + column, y + row))
                    continue;

                int top = source.At(column, row);
                int under = target.At(x + column, y + row);
                float weight = source.Pixels[top + Alpha] / Opaque * strength;

                for (int c = 0; c < Alpha; ++c)
                    target.Pixels[under + c] = Byte(MathF.Min(Opaque, target.Pixels[under + c] + source.Pixels[top + c] * weight));
            }
        }
    }

    public static void Faded(BgraImage image, float opacity)
    {
        for (int at = Alpha; at < image.Pixels.Length; at += Channels)
            image.Pixels[at] = Byte(image.Pixels[at] * opacity);
    }

    public static void Fill(BgraImage target, PixelRect area, uint bgra)
    {
        for (int y = area.Y; y < area.Y + area.Height; ++y)
        {
            for (int x = area.X; x < area.X + area.Width; ++x)
            {
                if (!target.Inside(x, y))
                    continue;

                for (int c = 0; c < Channels; ++c)
                    target.Pixels[target.At(x, y) + c] = (byte)(bgra >> (8 * c));
            }
        }
    }

    public static void Bleed(BgraImage image)
    {
        int width = image.Width;
        int height = image.Height;
        byte[] state = new byte[width * height];
        long[] sum = new long[Alpha];
        long count = 0;

        for (int at = 0; at < width * height; ++at)
        {
            int pixel = at * Channels;

            if (image.Pixels[pixel + Alpha] == 0)
                continue;

            state[at] = Filled;

            for (int c = 0; c < Alpha; ++c)
                sum[c] += image.Pixels[pixel + c];

            ++count;
        }

        if (count == 0)
            return;

        List<int> frontier = [];

        for (int at = 0; at < width * height; ++at)
        {
            if (state[at] == Filled)
                QueueNeighbours(image, state, at, frontier);
        }

        for (int pass = 0; pass < BleedPasses && frontier.Count > 0; ++pass)
        {
            foreach (int at in frontier)
                Blend(image, state, at);

            foreach (int at in frontier)
                state[at] = Filled;

            List<int> next = [];

            foreach (int at in frontier)
                QueueNeighbours(image, state, at, next);

            frontier = next;
        }

        for (int at = 0; at < width * height; ++at)
        {
            if (state[at] == Filled)
                continue;

            for (int c = 0; c < Alpha; ++c)
                image.Pixels[at * Channels + c] = (byte)(sum[c] / count);
        }
    }

    private static List<Tap>[] TapsFor(int from, int to) => to < from ? Shrinking(from, to) : Growing(from, to);

    private static List<Tap>[] Shrinking(int from, int to)
    {
        List<Tap>[] taps = new List<Tap>[to];
        float scale = (float)from / to;

        for (int target = 0; target < to; ++target)
        {
            taps[target] = [];
            float begin = target * scale;
            float end = begin + scale;

            for (int source = (int)begin; source < from && source < end; ++source)
            {
                float covered = MathF.Min(end, source + 1.0f) - MathF.Max(begin, source);

                if (covered > 0.0f)
                    taps[target].Add(new Tap(source, covered / scale));
            }
        }

        return taps;
    }

    private static List<Tap>[] Growing(int from, int to)
    {
        List<Tap>[] taps = new List<Tap>[to];
        float scale = (float)from / to;

        for (int target = 0; target < to; ++target)
        {
            float centre = (target + 0.5f) * scale - 0.5f;
            int left = (int)MathF.Floor(centre);
            float right = centre - left;

            taps[target] = [new Tap(Math.Clamp(left, 0, from - 1), 1.0f - right), new Tap(Math.Clamp(left + 1, 0, from - 1), right)];
        }

        return taps;
    }

    private static float[] Premultiplied(BgraImage source)
    {
        float[] premultiplied = new float[source.Pixels.Length];

        for (int at = 0; at < source.Pixels.Length; at += Channels)
        {
            float alpha = source.Pixels[at + Alpha] / Opaque;

            for (int c = 0; c < Alpha; ++c)
                premultiplied[at + c] = source.Pixels[at + c] * alpha;

            premultiplied[at + Alpha] = source.Pixels[at + Alpha];
        }

        return premultiplied;
    }

    private static byte Byte(float value) => (byte)Math.Clamp(value + 0.5f, 0.0f, Opaque);

    private static void Store(float[] premultiplied, byte[] pixels, int at)
    {
        float alpha = premultiplied[Alpha];
        pixels[at + Alpha] = Byte(alpha);

        if (alpha <= 0.0f)
        {
            pixels[at] = pixels[at + 1] = pixels[at + 2] = 0;
            return;
        }

        for (int c = 0; c < Alpha; ++c)
            pixels[at + c] = Byte(premultiplied[c] * Opaque / alpha);
    }

    private static void QueueNeighbours(BgraImage image, byte[] state, int at, List<int> queue)
    {
        int x = at % image.Width;
        int y = at / image.Width;

        foreach ((int stepX, int stepY) in Steps)
        {
            int nx = x + stepX;
            int ny = y + stepY;

            if (!image.Inside(nx, ny))
                continue;

            int next = ny * image.Width + nx;

            if (state[next] != Open)
                continue;

            state[next] = Queued;
            queue.Add(next);
        }
    }

    private static void Blend(BgraImage image, byte[] state, int at)
    {
        int x = at % image.Width;
        int y = at / image.Width;
        int[] total = new int[Alpha];
        int near = 0;

        foreach ((int stepX, int stepY) in Steps)
        {
            int nx = x + stepX;
            int ny = y + stepY;

            if (!image.Inside(nx, ny) || state[ny * image.Width + nx] != Filled)
                continue;

            int source = image.At(nx, ny);

            for (int c = 0; c < Alpha; ++c)
                total[c] += image.Pixels[source + c];

            ++near;
        }

        if (near == 0)
            return;

        int pixel = image.At(x, y);

        for (int c = 0; c < Alpha; ++c)
            image.Pixels[pixel + c] = (byte)(total[c] / near);
    }
}
