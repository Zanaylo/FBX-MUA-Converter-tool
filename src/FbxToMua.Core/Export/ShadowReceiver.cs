using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public sealed record ShadowChunk(Float3[] Positions, int[][] Parts);

public sealed record ShadowCandidate(ShadowChunk[] Chunks);

public sealed record ShadowPiece<T>(List<T> Vertices, List<List<int>> Parts)
{
    public bool IsEmpty => Parts.All(part => part.Count == 0);

    public static ShadowPiece<T> Empty(int parts) => new([], Enumerable.Range(0, parts).Select(_ => new List<int>()).ToList());

    public void Append(int part, SlabCut<T> cut)
    {
        int offset = Vertices.Count;
        Vertices.AddRange(cut.Vertices);
        Parts[part].AddRange(cut.Triangles.Select(index => index + offset));
    }
}

public sealed record ShadowSplit<T>(ShadowPiece<T> Lit, ShadowPiece<T> Unlit);

public static class ShadowReceiver
{
    private const int Depth = 2;
    private const int Height = 1;

    public static bool IsFloor(IReadOnlyList<ShadowChunk> chunks)
    {
        List<Float3> positions = [];
        List<int> triangles = [];

        foreach (ShadowChunk chunk in chunks)
        {
            int offset = positions.Count;
            positions.AddRange(chunk.Positions);
            triangles.AddRange(chunk.Parts.SelectMany(part => part).Select(index => index + offset));
        }

        return ShadowFloor.Catches(positions, triangles);
    }

    public static ShadowSplit<T> Split<T>(IReadOnlyList<T> vertices, IReadOnlyList<IReadOnlyList<int>> parts, int[] light, int mostVertices,
        Func<T, Float3> position, Func<T, T, float, T> between)
    {
        ShadowPiece<T> whole = new([.. vertices], parts.Select(part => part.ToList()).ToList());
        ShadowSplit<T> unlit = new(ShadowPiece<T>.Empty(parts.Count), whole);

        if (Takes(light, whole, position))
            return new ShadowSplit<T>(whole, ShadowPiece<T>.Empty(parts.Count));

        ShadowBand band = ShadowArea.Band(light);
        ShadowPiece<T> lit = ShadowPiece<T>.Empty(parts.Count);
        ShadowPiece<T> rest = ShadowPiece<T>.Empty(parts.Count);

        for (int p = 0; p < parts.Count; ++p)
        {
            (SlabCut<T> inside, SlabCut<T> outside) = DepthSlab.Cut(vertices, parts[p], band.Low, band.High, vertex => position(vertex)[Depth], between);
            lit.Append(p, inside);
            rest.Append(p, outside);
        }

        if (lit.IsEmpty || !Takes(light, lit, position))
            return unlit;

        if (lit.Vertices.Count > mostVertices || rest.Vertices.Count > mostVertices)
            return unlit;

        return new ShadowSplit<T>(lit, rest);
    }

    public static List<ShadowPiece<Float3>> Lit(IReadOnlyList<ShadowCandidate> candidates, Matrix place, int[] light)
    {
        List<ShadowPiece<Float3>> lit = [];

        foreach (ShadowCandidate candidate in candidates)
        {
            ShadowChunk[] placed = candidate.Chunks.Select(chunk => new ShadowChunk(chunk.Positions.Select(point => MatrixMath.Transform(point, place)).ToArray(), chunk.Parts)).ToArray();

            if (!IsFloor(placed))
                continue;

            foreach (ShadowChunk chunk in placed)
            {
                ShadowSplit<Float3> split = Split(chunk.Positions, chunk.Parts, light, MuaVertex.MostPerMesh, point => point, Float3.Lerp);

                if (!split.Lit.IsEmpty)
                    lit.Add(split.Lit);
            }
        }

        return lit;
    }

    private static bool Takes<T>(int[] light, ShadowPiece<T> piece, Func<T, Float3> position)
    {
        if (piece.Vertices.Count == 0)
            return false;

        float lowY = float.MaxValue;
        float lowZ = float.MaxValue;
        float highZ = float.MinValue;

        foreach (T vertex in piece.Vertices)
        {
            Float3 point = position(vertex);
            lowY = Math.Min(lowY, point[Height]);
            lowZ = Math.Min(lowZ, point[Depth]);
            highZ = Math.Max(highZ, point[Depth]);
        }

        return ShadowArea.Takes(light, lowY, lowZ, highZ);
    }
}
