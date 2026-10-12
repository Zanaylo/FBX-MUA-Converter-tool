using FbxToMua.Core.Imaging;

namespace FbxToMua.Core.Export;

public static class GameBloomShield
{
    public const byte HiddenAlpha = 1;
    private const byte OpaqueAlpha = byte.MaxValue;

    public static double UnwrittenFront(IReadOnlyList<DepthSpan> spans, IReadOnlyList<FarPull> pulls)
    {
        double front = double.MaxValue;

        for (int i = 0; i < spans.Count; ++i)
        {
            if (!pulls[i].KeepsDepth)
                front = Math.Min(front, spans[i].Nearest * pulls[i].Factor);
        }

        return front;
    }

    public static bool DrawsFirstSafely(DepthSpan span, FarPull pull, double unwrittenFront)
    {
        return pull.KeepsDepth && span.Farthest * pull.Factor < unwrittenFront;
    }

    public static bool IsSolid(BgraImage image)
    {
        for (int at = BgraImage.Alpha; at < image.Pixels.Length; at += BgraImage.Channels)
        {
            if (image.Pixels[at] != OpaqueAlpha)
                return false;
        }

        return true;
    }
}
