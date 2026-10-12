using FbxToMua.Core.Imaging;

namespace FbxToMua.Core.Export;

public static class StageBloom
{
    private const int Shrink = 4;
    private const int Colours = 3;
    private const float BloomBufferWidth = 256.0f;
    private const float NarrowestSpread = 0.5f;
    private const float KernelReach = 3.0f;
    private const float Opaque = 255.0f;
    private const float Half = 0.5f;

    public static BgraImage Glowed(BgraImage image, BloomLook bloom)
    {
        int width = Math.Max(1, image.Width / Shrink);
        int height = Math.Max(1, image.Height / Shrink);
        float[] glow = Source(image, bloom, width, height);
        float spread = Math.Max(NarrowestSpread, bloom.Radius * width / BloomBufferWidth);
        float[] kernel = Kernel(spread);
        glow = Blurred(glow, width, height, kernel, true);
        glow = Blurred(glow, width, height, kernel, false);

        return Added(image, glow, width, height, bloom.Strength);
    }

    private static float[] Source(BgraImage image, BloomLook bloom, int width, int height)
    {
        float[] bias = [bloom.Blue, bloom.Green, bloom.Red];
        float[] glow = new float[width * height * Colours];
        int[] taken = new int[width * height];

        for (int y = 0; y < image.Height; ++y)
        {
            int row = Math.Min(height - 1, y / Shrink);

            for (int x = 0; x < image.Width; ++x)
            {
                int cell = row * width + Math.Min(width - 1, x / Shrink);
                int at = image.At(x, y);
                float alpha = image.Pixels[at + BgraImage.Alpha] / Opaque;

                for (int c = 0; c < Colours; ++c)
                    glow[cell * Colours + c] += MathF.Pow(image.Pixels[at + c] / Opaque * bloom.Brightness, bloom.Power) * bias[c] * alpha;

                ++taken[cell];
            }
        }

        for (int cell = 0; cell < taken.Length; ++cell)
        {
            for (int c = 0; c < Colours; ++c)
                glow[cell * Colours + c] /= Math.Max(1, taken[cell]);
        }

        return glow;
    }

    private static float[] Kernel(float spread)
    {
        int reach = (int)MathF.Ceiling(KernelReach * spread);
        float[] kernel = new float[reach * 2 + 1];

        for (int k = -reach; k <= reach; ++k)
            kernel[k + reach] = MathF.Exp(-k * k / (2.0f * spread * spread));

        float sum = kernel.Sum();

        return kernel.Select(weight => weight / sum).ToArray();
    }

    private static float[] Blurred(float[] glow, int width, int height, float[] kernel, bool across)
    {
        int reach = kernel.Length / 2;
        float[] blurred = new float[glow.Length];

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                for (int k = -reach; k <= reach; ++k)
                {
                    int sx = across ? Math.Clamp(x + k, 0, width - 1) : x;
                    int sy = across ? y : Math.Clamp(y + k, 0, height - 1);

                    for (int c = 0; c < Colours; ++c)
                        blurred[(y * width + x) * Colours + c] += glow[(sy * width + sx) * Colours + c] * kernel[k + reach];
                }
            }
        }

        return blurred;
    }

    private static BgraImage Added(BgraImage image, float[] glow, int width, int height, float strength)
    {
        BgraImage glowed = image.Copy();

        for (int y = 0; y < image.Height; ++y)
        {
            float gy = Math.Clamp((y + Half) / Shrink - Half, 0.0f, height - 1);

            for (int x = 0; x < image.Width; ++x)
            {
                float gx = Math.Clamp((x + Half) / Shrink - Half, 0.0f, width - 1);
                int at = glowed.At(x, y);

                for (int c = 0; c < Colours; ++c)
                {
                    float added = glowed.Pixels[at + c] / Opaque + Sampled(glow, width, height, gx, gy, c) * strength;
                    glowed.Pixels[at + c] = (byte)MathF.Round(Math.Clamp(added, 0.0f, 1.0f) * Opaque, MidpointRounding.AwayFromZero);
                }
            }
        }

        return glowed;
    }

    private static float Sampled(float[] glow, int width, int height, float x, float y, int colour)
    {
        int left = (int)x;
        int top = (int)y;
        int right = Math.Min(width - 1, left + 1);
        int bottom = Math.Min(height - 1, top + 1);
        float across = x - left;
        float down = y - top;
        float upper = glow[(top * width + left) * Colours + colour] * (1.0f - across) + glow[(top * width + right) * Colours + colour] * across;
        float lower = glow[(bottom * width + left) * Colours + colour] * (1.0f - across) + glow[(bottom * width + right) * Colours + colour] * across;

        return upper * (1.0f - down) + lower * down;
    }
}
