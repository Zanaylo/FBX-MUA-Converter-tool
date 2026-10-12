using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Export;

public class ShadowReceiverUnitTest
{
    private const int NoLimit = int.MaxValue;
    private static readonly int[] ShippedLight = [0, 900, -1000];
    private static readonly int[] LeaningLight = [-326, 690, -946];
    private static readonly int[][] QuadParts = [[0, 2, 1, 0, 3, 2]];

    private static Float3[] Ground(float height, float near, float far)
    {
        return [new(-3000.0f, height, near), new(3000.0f, height, near), new(3000.0f, height, far), new(-3000.0f, height, far)];
    }

    private static Float3[] Wall()
    {
        return [new(-300.0f, 0.0f, 50.0f), new(300.0f, 0.0f, 50.0f), new(300.0f, 400.0f, 50.0f), new(-300.0f, 400.0f, 50.0f)];
    }

    private static ShadowSplit<Float3> Split(Float3[] positions, int[] light, int mostVertices = NoLimit)
    {
        return ShadowReceiver.Split(positions, QuadParts, light, mostVertices, position => position, Float3.Lerp);
    }

    private static (float Low, float High) DepthsOf(ShadowPiece<Float3> piece)
    {
        IEnumerable<float> depths = piece.Parts.SelectMany(part => part).Select(index => piece.Vertices[index][2]);

        return (depths.Min(), depths.Max());
    }

    [Fact]
    public void Split_ShippedLightOnAWideFloor_LightsTheWholeFloor()
    {
        // Arrange
        Float3[] floor = Ground(0.0f, -2800.0f, 1500.0f);

        // Act
        ShadowSplit<Float3> split = Split(floor, ShippedLight);

        // Assert
        Assert.Equal(floor, split.Lit.Vertices);
        Assert.True(split.Unlit.IsEmpty);
    }

    [Fact]
    public void Split_LeaningLightOnAWideFloor_LightsTheStripTheGameTakes()
    {
        // Arrange
        Float3[] floor = Ground(0.0f, -2800.0f, 1500.0f);
        ShadowBand band = ShadowArea.Band(LeaningLight);

        // Act
        ShadowSplit<Float3> split = Split(floor, LeaningLight);

        // Assert
        (float low, float high) = DepthsOf(split.Lit);
        Assert.Equal(band.Low, low, 1e-2f);
        Assert.Equal(band.High, high, 1e-2f);
        Assert.True(ShadowArea.Takes(LeaningLight, 0.0f, low, high));
        Assert.False(split.Unlit.IsEmpty);
    }

    [Fact]
    public void Split_FloorAboveTheShadowArea_LightsNothing()
    {
        // Arrange
        Float3[] roof = Ground(2000.0f, -2800.0f, 1500.0f);

        // Act
        ShadowSplit<Float3> split = Split(roof, LeaningLight);

        // Assert
        Assert.True(split.Lit.IsEmpty);
        Assert.Equal(roof, split.Unlit.Vertices);
    }

    [Fact]
    public void Split_PiecesOverTheVertexLimit_LightsNothing()
    {
        // Arrange
        Float3[] floor = Ground(0.0f, -2800.0f, 1500.0f);

        // Act
        ShadowSplit<Float3> split = Split(floor, LeaningLight, floor.Length);

        // Assert
        Assert.True(split.Lit.IsEmpty);
        Assert.Equal(floor, split.Unlit.Vertices);
    }

    [Fact]
    public void IsFloor_GroundUnderTheFighters_True()
    {
        // Arrange
        ShadowChunk[] chunks = [new(Ground(0.0f, -500.0f, 500.0f), QuadParts)];

        // Act
        bool floor = ShadowReceiver.IsFloor(chunks);

        // Assert
        Assert.True(floor);
    }

    [Fact]
    public void IsFloor_StandingWall_False()
    {
        // Arrange
        ShadowChunk[] chunks = [new(Wall(), QuadParts)];

        // Act
        bool floor = ShadowReceiver.IsFloor(chunks);

        // Assert
        Assert.False(floor);
    }

    [Fact]
    public void Lit_FloorAndWall_LightsOnlyTheFloorWhereThePlacementPutsIt()
    {
        // Arrange
        ShadowCandidate floor = new([new(Ground(0.0f, -500.0f, 500.0f), QuadParts)]);
        ShadowCandidate wall = new([new(Wall(), QuadParts)]);
        Matrix lowered = Matrix.Identity;
        lowered[13] = -5.0f;

        // Act
        List<ShadowPiece<Float3>> lit = ShadowReceiver.Lit([floor, wall], lowered, ShippedLight);

        // Assert
        ShadowPiece<Float3> piece = Assert.Single(lit);
        Assert.All(piece.Vertices, vertex => Assert.Equal(-5.0f, vertex[1]));
    }

    [Fact]
    public void Lit_FloorLiftedOutOfReach_LightsNothing()
    {
        // Arrange
        ShadowCandidate floor = new([new(Ground(0.0f, -500.0f, 500.0f), QuadParts)]);
        Matrix lifted = Matrix.Identity;
        lifted[13] = 50.0f;

        // Act
        List<ShadowPiece<Float3>> lit = ShadowReceiver.Lit([floor], lifted, ShippedLight);

        // Assert
        Assert.Empty(lit);
    }
}
