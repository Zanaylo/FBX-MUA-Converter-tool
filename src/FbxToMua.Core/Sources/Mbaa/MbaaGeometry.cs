using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Import;

namespace FbxToMua.Core.Sources.Mbaa;

internal readonly record struct MbaaBox(float Left, float Right, float Top, float Bottom, float Depth)
{
    public MbaaBox Shifted(float by) => this with { Left = Left + by, Right = Right + by };
}

internal readonly record struct MbaaCorners(float ULeft, float URight, float VTop, float VBottom)
{
    public MbaaCorners Mirrored() => new(URight, ULeft, VTop, VBottom);
}

internal static class MbaaGeometry
{
    public const float Fov = 30.0f;
    public const int OriginX = 128;
    public const int OriginY = 224;

    private const float PixelsPerUnit = 240.0f;
    private const float EyeHeight = 280.0f / 360.0f;
    private const float NearestParallax = 0.05f;
    private const float FarthestParallax = 4.0f;
    private const float Pi = 3.14159265358979f;

    private static readonly int[] FrontFace = [0, 2, 1, 0, 3, 2];
    private static readonly int[] BackFace = [0, 1, 2, 0, 2, 3];

    public static MbaaBox Place(int parallax, float left, float top, float width, float height)
    {
        float share = Math.Clamp((float)parallax / MbaaBg.FullParallax, NearestParallax, FarthestParallax);

        float Across(float pixels) => (pixels - OriginX) / PixelsPerUnit / share;

        float Up(float pixels) => EyeHeight + (-(pixels - OriginY) / PixelsPerUnit - EyeHeight) / share;

        return new MbaaBox(Across(left), Across(left + width), Up(top), Up(top + height), -Lens() * (1.0f - share) / share);
    }

    public static MbaaBox Unit() => new(0.0f, 1.0f, 0.0f, 1.0f, 0.0f);

    public static MbaaCorners Sheet(float width, float height) => new(0.0f, width, 1.0f, 1.0f - height);

    public static MbaaCorners Flip(int slot, int lamp)
    {
        float u = -ImMarks.FlipMark * (slot + 1);
        float v = lamp < 0 ? 0.0f : -ImMarks.LampMark * (lamp + 1);

        return new MbaaCorners(u + Squeezed(0.0f), u + Squeezed(1.0f), v + Squeezed(1.0f), v + Squeezed(0.0f));
    }

    public static void PushQuad(FbxExNode node, MbaaBox box, float alpha, MbaaCorners corners)
    {
        if (node.Submeshes.Count == 0)
            return;

        int first = node.VertexCount;
        PushVertex(node.Vertices, box.Left, box.Top, box.Depth, alpha, corners.ULeft, corners.VTop);
        PushVertex(node.Vertices, box.Right, box.Top, box.Depth, alpha, corners.URight, corners.VTop);
        PushVertex(node.Vertices, box.Right, box.Bottom, box.Depth, alpha, corners.URight, corners.VBottom);
        PushVertex(node.Vertices, box.Left, box.Bottom, box.Depth, alpha, corners.ULeft, corners.VBottom);

        List<int> indices = node.Submeshes[0].Indices;

        foreach (int corner in FrontFace)
            indices.Add(first + corner);

        foreach (int corner in BackFace)
            indices.Add(first + corner);
    }

    public static List<float> Matrix(MbaaBox box)
    {
        return
        [
            box.Right - box.Left, 0.0f, 0.0f, 0.0f,
            0.0f, box.Bottom - box.Top, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f,
            box.Left, box.Top, box.Depth, 1.0f,
        ];
    }

    public static List<float> Rest() => Matrix(Unit());

    private static float Lens() => 1.0f / MathF.Tan(Fov * Pi / 360.0f);

    private static float Squeezed(float local) => ImMarks.FlipInset + ImMarks.FlipSpan * local;

    private static void PushVertex(List<float> vertices, float x, float y, float z, float alpha, float u, float v)
    {
        vertices.AddRange([x, y, z, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 1.0f, alpha, u, v]);
    }
}
