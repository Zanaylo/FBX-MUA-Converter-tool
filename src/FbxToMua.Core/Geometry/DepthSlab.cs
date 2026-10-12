namespace FbxToMua.Core.Geometry;

public sealed record SlabCut<T>(List<T> Vertices, List<int> Triangles)
{
    public static SlabCut<T> Empty() => new([], []);
}

public static class DepthSlab
{
    private const int Corners = 3;
    private const int Original = -1;

    private readonly record struct SlabPoint(int From, int To, float Level)
    {
        public static SlabPoint Vertex(int index) => new(index, Original, 0.0f);

        public bool IsVertex => To == Original;
    }

    private sealed class Piece<T>(IReadOnlyList<T> source, Func<T, float> depth, Func<T, T, float, T> between)
    {
        private readonly Dictionary<SlabPoint, int> _taken = [];

        public SlabCut<T> Cut { get; } = SlabCut<T>.Empty();

        public void Add(List<SlabPoint> polygon)
        {
            for (int k = 1; k + 1 < polygon.Count; ++k)
            {
                Cut.Triangles.Add(IndexOf(polygon[0]));
                Cut.Triangles.Add(IndexOf(polygon[k]));
                Cut.Triangles.Add(IndexOf(polygon[k + 1]));
            }
        }

        private int IndexOf(SlabPoint point)
        {
            if (_taken.TryGetValue(point, out int known))
                return known;

            int index = Cut.Vertices.Count;
            Cut.Vertices.Add(point.IsVertex ? source[point.From] : Crossing(point));
            _taken[point] = index;

            return index;
        }

        private T Crossing(SlabPoint point)
        {
            T from = source[point.From];
            T to = source[point.To];
            float near = depth(from);
            float span = depth(to) - near;

            return between(from, to, span == 0.0f ? 0.0f : (point.Level - near) / span);
        }
    }

    public static (SlabCut<T> Inside, SlabCut<T> Outside) Cut<T>(IReadOnlyList<T> vertices, IReadOnlyList<int> triangles, float low, float high,
        Func<T, float> depth, Func<T, T, float, T> between)
    {
        Piece<T> inside = new(vertices, depth, between);
        Piece<T> outside = new(vertices, depth, between);
        float[] depths = vertices.Select(depth).ToArray();

        for (int i = 0; i + Corners - 1 < triangles.Count; i += Corners)
        {
            List<SlabPoint> triangle = [SlabPoint.Vertex(triangles[i]), SlabPoint.Vertex(triangles[i + 1]), SlabPoint.Vertex(triangles[i + 2])];
            List<SlabPoint> fromLow = Clipped(triangle, depths, low, true);

            inside.Add(Clipped(fromLow, depths, high, false));
            outside.Add(Clipped(triangle, depths, low, false));
            outside.Add(Clipped(fromLow, depths, high, true));
        }

        return (inside.Cut, outside.Cut);
    }

    private static List<SlabPoint> Clipped(List<SlabPoint> polygon, float[] depths, float level, bool keepAbove)
    {
        List<SlabPoint> kept = [];

        for (int k = 0; k < polygon.Count; ++k)
        {
            SlabPoint current = polygon[k];
            SlabPoint next = polygon[(k + 1) % polygon.Count];
            bool currentIn = Keeps(DepthOf(current, depths), level, keepAbove);
            bool nextIn = Keeps(DepthOf(next, depths), level, keepAbove);

            if (currentIn)
                kept.Add(current);

            if (currentIn != nextIn)
                kept.Add(CrossingOf(current, next, level));
        }

        return kept;
    }

    private static bool Keeps(float depth, float level, bool keepAbove) => keepAbove ? depth >= level : depth <= level;

    private static float DepthOf(SlabPoint point, float[] depths) => point.IsVertex ? depths[point.From] : point.Level;

    private static SlabPoint CrossingOf(SlabPoint current, SlabPoint next, float level)
    {
        (int a, int b) = EdgeUnder(current, next);

        return new SlabPoint(Math.Min(a, b), Math.Max(a, b), level);
    }

    private static (int A, int B) EdgeUnder(SlabPoint current, SlabPoint next)
    {
        if (!current.IsVertex)
            return (current.From, current.To);

        if (!next.IsVertex)
            return (next.From, next.To);

        return (current.From, next.From);
    }
}
