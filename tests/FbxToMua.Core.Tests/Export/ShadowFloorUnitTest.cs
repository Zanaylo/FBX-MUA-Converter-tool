using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Export;

public class ShadowFloorUnitTest
{
    private static readonly List<int> Quad = [0, 2, 1, 0, 3, 2];

    private static Float3 At(float x, float y, float z) => new(x, y, z);

    private static List<Float3> Ground(float height, float half)
    {
        return [At(-half, height, -half), At(half, height, -half), At(half, height, half), At(-half, height, half)];
    }

    private static List<Float3> Wall(float width, float height)
    {
        return [At(-width, 0.0f, 50.0f), At(width, 0.0f, 50.0f), At(width, height, 50.0f), At(-width, height, 50.0f)];
    }

    [Fact]
    public void Catches_FloorUnderTheFighters_True()
    {
        // Arrange
        List<Float3> floor = Ground(0.0f, 300.0f);

        // Act
        bool catches = ShadowFloor.Catches(floor, Quad);

        // Assert
        Assert.True(catches);
    }

    [Fact]
    public void Catches_StandingWall_False()
    {
        // Arrange
        List<Float3> wall = Wall(300.0f, 200.0f);

        // Act
        bool catches = ShadowFloor.Catches(wall, Quad);

        // Assert
        Assert.False(catches);
    }

    [Fact]
    public void Catches_RoofAboveTheFighters_False()
    {
        // Arrange
        List<Float3> roof = Ground(150.0f, 300.0f);

        // Act
        bool catches = ShadowFloor.Catches(roof, Quad);

        // Assert
        Assert.False(catches);
    }

    [Fact]
    public void Catches_FloorWithASmallerKerb_True()
    {
        // Arrange
        List<Float3> vertices = [.. Ground(0.0f, 300.0f), .. Wall(300.0f, 20.0f)];
        List<int> triangles = [.. Quad, .. Quad.Select(index => index + 4)];

        // Act
        bool catches = ShadowFloor.Catches(vertices, triangles);

        // Assert
        Assert.True(catches);
    }

    [Fact]
    public void Catches_BowlRisingAwayFromTheFighters_True()
    {
        // Arrange
        List<Float3> bowl = [At(-300.0f, -150.0f, -300.0f), At(300.0f, -150.0f, -300.0f), At(300.0f, 150.0f, 300.0f), At(-300.0f, 150.0f, 300.0f)];

        // Act
        bool catches = ShadowFloor.Catches(bowl, Quad);

        // Assert
        Assert.True(catches);
    }

    [Fact]
    public void Catches_FloorBehindTheFightLine_False()
    {
        // Arrange
        List<Float3> yard = [At(-300.0f, 0.0f, 200.0f), At(300.0f, 0.0f, 200.0f), At(300.0f, 0.0f, 800.0f), At(-300.0f, 0.0f, 800.0f)];

        // Act
        bool catches = ShadowFloor.Catches(yard, Quad);

        // Assert
        Assert.False(catches);
    }

    [Fact]
    public void Catches_NoTriangles_False()
    {
        // Arrange
        List<Float3> floor = Ground(0.0f, 300.0f);

        // Act
        bool catches = ShadowFloor.Catches(floor, []);

        // Assert
        Assert.False(catches);
    }
}
