namespace FbxToMua.Core.Geometry;

public static class TriangleStrip
{
    public static List<ushort> Build(IReadOnlyList<int> triangles)
    {
        List<ushort> strip = [];

        for (int i = 0; i + 2 < triangles.Count; i += 3)
        {
            int[] corner = [triangles[i], triangles[i + 1], triangles[i + 2]];

            if (TryExtend(strip, corner, out int added))
            {
                strip.Add((ushort)added);
                continue;
            }

            Restart(strip, corner);
        }

        return strip;
    }

    private static bool Odd(List<ushort> strip) => strip.Count % 2 != 0;

    private static bool TryExtend(List<ushort> strip, int[] corner, out int added)
    {
        added = 0;

        if (strip.Count < 2)
            return false;

        int first = strip[^2];
        int second = strip[^1];

        for (int turn = 0; turn < 3; ++turn)
        {
            int a = corner[turn];
            int b = corner[(turn + 1) % 3];
            int c = corner[(turn + 2) % 3];

            bool even = !Odd(strip) && first == a && second == b;
            bool odd = Odd(strip) && first == a && second == c;

            if (!even && !odd)
                continue;

            added = even ? c : b;
            return true;
        }

        return false;
    }

    private static void Restart(List<ushort> strip, int[] corner)
    {
        if (strip.Count > 0)
        {
            strip.Add(strip[^1]);
            strip.Add((ushort)corner[0]);

            if (Odd(strip))
                strip.Add((ushort)corner[0]);
        }

        strip.Add((ushort)corner[0]);
        strip.Add((ushort)corner[1]);
        strip.Add((ushort)corner[2]);
    }
}
