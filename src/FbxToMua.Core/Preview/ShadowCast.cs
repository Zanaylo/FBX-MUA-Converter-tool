using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Preview;

public readonly record struct ShadowCaster(float X, float Width, float Height, float FallX, float FallZ);

public sealed record ShadowSurface(Float3[] Positions, float[] Uvs, int[] Triangles)
{
    public static ShadowSurface Empty => new([], [], []);
}

public static class ShadowCast
{
    private const float Flat = 1e-6f;
    private const float Half = 0.5f;
    private const int Depth = 2;
    private const int Rise = 1;
    private const int Across = 0;

    private readonly record struct ShadowVertex(Float3 Position, float Drop, float U, float V)
    {
        public static ShadowVertex Between(ShadowVertex from, ShadowVertex to, float share)
        {
            return new ShadowVertex(Float3.Lerp(from.Position, to.Position, share), from.Drop + (to.Drop - from.Drop) * share,
                from.U + (to.U - from.U) * share, from.V + (to.V - from.V) * share);
        }
    }

    public static ShadowSurface Onto(IReadOnlyList<ShadowPiece<Float3>> lit, Matrix toWorld, ShadowCaster caster)
    {
        if (MathF.Abs(caster.FallZ) < Flat)
            return ShadowSurface.Empty;

        List<Float3> positions = [];
        List<float> uvs = [];
        List<int> triangles = [];

        foreach (ShadowPiece<Float3> piece in lit)
        {
            List<ShadowVertex> vertices = piece.Vertices.Select(point => Mapped(MatrixMath.Transform(point, toWorld), caster)).ToList();
            SlabCut<ShadowVertex> covered = Covered(vertices, piece.Parts.SelectMany(part => part).ToList());
            int offset = positions.Count;

            positions.AddRange(covered.Vertices.Select(vertex => vertex.Position));
            uvs.AddRange(covered.Vertices.SelectMany(vertex => new[] { vertex.U, vertex.V }));
            triangles.AddRange(covered.Triangles.Select(index => index + offset));
        }

        return new ShadowSurface([.. positions], [.. uvs], [.. triangles]);
    }

    private static ShadowVertex Mapped(Float3 point, ShadowCaster caster)
    {
        float drop = point[Depth] / caster.FallZ;
        float height = point[Rise] + drop;
        float across = point[Across] - caster.X - caster.FallX * drop;

        return new ShadowVertex(point, drop, across / caster.Width + Half, 1.0f - height / caster.Height);
    }

    private static SlabCut<ShadowVertex> Covered(List<ShadowVertex> vertices, List<int> triangles)
    {
        SlabCut<ShadowVertex> cut = DepthSlab.Cut(vertices, triangles, 0.0f, float.PositiveInfinity, vertex => vertex.Drop, ShadowVertex.Between).Inside;
        cut = DepthSlab.Cut(cut.Vertices, cut.Triangles, 0.0f, 1.0f, vertex => vertex.U, ShadowVertex.Between).Inside;

        return DepthSlab.Cut(cut.Vertices, cut.Triangles, 0.0f, 1.0f, vertex => vertex.V, ShadowVertex.Between).Inside;
    }
}
