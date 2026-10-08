using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Geometry;

public class TriangleStripUnitTest
{
    public static List<string> Unrolled(IReadOnlyList<ushort> strip)
    {
        List<string> triangles = [];

        for (int i = 0; i + 2 < strip.Count; ++i)
        {
            int a = strip[i];
            int b = strip[i + 1];
            int c = strip[i + 2];

            if (a == b || b == c || a == c)
                continue;

            triangles.Add(i % 2 != 0 ? Canonical(a, c, b) : Canonical(a, b, c));
        }

        triangles.Sort(StringComparer.Ordinal);
        return triangles;
    }

    public static List<string> Listed(IReadOnlyList<int> list)
    {
        List<string> triangles = [];

        for (int i = 0; i + 2 < list.Count; i += 3)
            triangles.Add(Canonical(list[i], list[i + 1], list[i + 2]));

        triangles.Sort(StringComparer.Ordinal);
        return triangles;
    }

    private static string Canonical(int a, int b, int c)
    {
        int[] corners = [a, b, c];
        int lowest = Array.IndexOf(corners, corners.Min());

        return $"{corners[lowest]},{corners[(lowest + 1) % 3]},{corners[(lowest + 2) % 3]}";
    }

    [Fact]
    public void Build_Quads_UnrollsToTheSameWoundTriangles()
    {
        // Arrange
        int[] quads = [0, 1, 2, 0, 2, 3, 2, 1, 4, 5, 6, 7, 5, 7, 8, 8, 7, 6];

        // Act
        List<ushort> strip = TriangleStrip.Build(quads);

        // Assert
        Assert.Equal(Listed(quads), Unrolled(strip));
        Assert.True(strip.Count < quads.Length / 3 * 5);
    }

    [Fact]
    public void Build_RandomSoup_SurvivesTheStrip()
    {
        // Arrange
        List<int> valid = [];
        uint seed = 12345u;
        List<int> random = [];

        for (int i = 0; i < 3000; ++i)
        {
            seed = unchecked(seed * 1103515245u + 12345u);
            random.Add((int)((seed >> 8) % 500));
        }

        for (int i = 0; i + 2 < random.Count; i += 3)
        {
            if (random[i] == random[i + 1] || random[i + 1] == random[i + 2] || random[i] == random[i + 2])
                continue;

            valid.AddRange(random.GetRange(i, 3));
        }

        // Act
        List<ushort> strip = TriangleStrip.Build(valid);

        // Assert
        Assert.Equal(Listed(valid), Unrolled(strip));
    }

    [Fact]
    public void Build_NoTriangles_GivesNoStrip()
    {
        // Arrange
        List<int> none = [];

        // Act
        List<ushort> strip = TriangleStrip.Build(none);

        // Assert
        Assert.Empty(strip);
    }
}
