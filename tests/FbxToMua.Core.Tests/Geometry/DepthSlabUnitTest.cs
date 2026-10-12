using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Geometry;

public class DepthSlabUnitTest
{
    private const float Near = -100.0f;
    private const float Far = 100.0f;
    private static readonly List<int> Quad = [0, 2, 1, 0, 3, 2];

    private static List<Float3> Ground(float near, float far)
    {
        return [new(-300.0f, 0.0f, near), new(300.0f, 0.0f, near), new(300.0f, 0.0f, far), new(-300.0f, 0.0f, far)];
    }

    private static (SlabCut<Float3> Inside, SlabCut<Float3> Outside) Cut(List<Float3> vertices, List<int> triangles)
    {
        return DepthSlab.Cut(vertices, triangles, Near, Far, vertex => vertex[2], Float3.Lerp);
    }

    private static double Area(SlabCut<Float3> cut)
    {
        double area = 0.0;

        for (int i = 0; i + 2 < cut.Triangles.Count; i += 3)
        {
            Float3 a = cut.Vertices[cut.Triangles[i]];
            Float3 b = cut.Vertices[cut.Triangles[i + 1]];
            Float3 c = cut.Vertices[cut.Triangles[i + 2]];
            area += Math.Abs((b[0] - a[0]) * (c[2] - a[2]) - (c[0] - a[0]) * (b[2] - a[2])) * 0.5;
        }

        return area;
    }

    private static double Rise(SlabCut<Float3> cut, int at)
    {
        Float3 a = cut.Vertices[cut.Triangles[at]];
        Float3 b = cut.Vertices[cut.Triangles[at + 1]];
        Float3 c = cut.Vertices[cut.Triangles[at + 2]];

        return (b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]);
    }

    [Fact]
    public void Cut_FloorAcrossTheStrip_KeepsOnlyTheStripInside()
    {
        // Arrange
        List<Float3> floor = Ground(-300.0f, 300.0f);

        // Act
        (SlabCut<Float3> inside, _) = Cut(floor, Quad);

        // Assert
        Assert.All(inside.Triangles, index => Assert.InRange(inside.Vertices[index][2], -100.0f, 100.0f));
        Assert.Equal(600.0 * 200.0, Area(inside), 3);
    }

    [Fact]
    public void Cut_FloorAcrossTheStrip_KeepsTheRestOutside()
    {
        // Arrange
        List<Float3> floor = Ground(-300.0f, 300.0f);

        // Act
        (_, SlabCut<Float3> outside) = Cut(floor, Quad);

        // Assert
        Assert.All(outside.Triangles, index => Assert.True(Math.Abs(outside.Vertices[index][2]) >= 100.0f));
        Assert.Equal(600.0 * 400.0, Area(outside), 3);
    }

    [Fact]
    public void Cut_FloorAcrossTheStrip_BothSidesMeetOnTheSamePoints()
    {
        // Arrange
        List<Float3> floor = [new(-300.0f, 0.0f, -300.0f), new(250.0f, 7.0f, -280.0f), new(310.0f, 3.0f, 290.0f), new(-290.0f, 11.0f, 300.0f)];

        // Act
        (SlabCut<Float3> inside, SlabCut<Float3> outside) = Cut(floor, Quad);

        // Assert
        List<Float3> seam = inside.Vertices.Where(vertex => Math.Abs(vertex[2]) == 100.0f).ToList();
        Assert.NotEmpty(seam);
        Assert.All(seam, vertex => Assert.Contains(vertex, outside.Vertices));
    }

    [Fact]
    public void Cut_FloorAcrossTheStrip_KeepsTheWinding()
    {
        // Arrange
        List<Float3> floor = Ground(-300.0f, 300.0f);
        SlabCut<Float3> whole = new(floor, Quad);
        double facing = Math.Sign(Rise(whole, 0));

        // Act
        (SlabCut<Float3> inside, SlabCut<Float3> outside) = Cut(floor, Quad);

        // Assert
        Assert.All(Enumerable.Range(0, inside.Triangles.Count / 3), t => Assert.Equal(facing, Math.Sign(Rise(inside, t * 3))));
        Assert.All(Enumerable.Range(0, outside.Triangles.Count / 3), t => Assert.Equal(facing, Math.Sign(Rise(outside, t * 3))));
    }

    [Fact]
    public void Cut_FloorInsideTheStrip_LeavesNothingOutside()
    {
        // Arrange
        List<Float3> floor = Ground(-50.0f, 50.0f);

        // Act
        (SlabCut<Float3> inside, SlabCut<Float3> outside) = Cut(floor, Quad);

        // Assert
        Assert.Equal(Quad.Count, inside.Triangles.Count);
        Assert.Empty(outside.Triangles);
    }

    [Fact]
    public void Cut_FloorOutsideTheStrip_LeavesNothingInside()
    {
        // Arrange
        List<Float3> floor = Ground(200.0f, 500.0f);

        // Act
        (SlabCut<Float3> inside, SlabCut<Float3> outside) = Cut(floor, Quad);

        // Assert
        Assert.Empty(inside.Triangles);
        Assert.Equal(Quad.Count, outside.Triangles.Count);
    }

    [Fact]
    public void Lerp_HalfWay_IsTheMiddle()
    {
        // Arrange
        Float3 from = new(0.0f, 10.0f, -4.0f);
        Float3 to = new(2.0f, 20.0f, 4.0f);

        // Act
        Float3 middle = Float3.Lerp(from, to, 0.5f);

        // Assert
        Assert.Equal(new Float3(1.0f, 15.0f, 0.0f), middle);
    }
}
