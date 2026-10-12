using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Export;

public class DraftMergerUnitTest
{
    private const int Stage = 1;
    private const int Pulled = 0;

    private static readonly DraftOwner Still = new(MuaBlend.Unset, MuaFlags.Static, null);
    private static readonly DraftOwner Glowing = new(MuaBlend.Add, MuaFlags.Static, null);

    private static MeshDraft Triangle(string name, DraftOwner owner, int order, int material, int tier = Stage, int vertices = 3)
    {
        List<MuaVertex> corners = Enumerable.Range(0, vertices).Select(v =>
        {
            MuaVertex vertex = MuaVertex.Unrigged();
            vertex.Position = new Float3(order, v, 0.0f);

            return vertex;
        }).ToList();

        return new MeshDraft
        {
            Name = name,
            Owner = owner,
            Tier = tier,
            Order = order,
            Vertices = corners,
            Parts = [(material, [0, 1, 2])],
        };
    }

    [Fact]
    public void Merge_NeighboursWithTheSameOwner_BecomeOneMeshWithIndicesMoved()
    {
        // Arrange
        List<MeshDraft> drafts = [Triangle("node_001", Still, 0, 5), Triangle("node_002", Still, 1, 5)];

        // Act
        List<MeshDraft> merged = DraftMerger.Merge(drafts);

        // Assert
        MeshDraft mesh = Assert.Single(merged);
        Assert.Equal("node_001", mesh.Name);
        Assert.Equal(6, mesh.Vertices.Count);
        (int material, List<int> indices) = Assert.Single(mesh.Parts);
        Assert.Equal(5, material);
        Assert.Equal([0, 1, 2, 3, 4, 5], indices);
        Assert.Equal(1.0f, mesh.Vertices[3].Position[0]);
    }

    [Fact]
    public void Merge_AnotherMaterial_KeepsItsOwnPartInOrder()
    {
        // Arrange
        List<MeshDraft> drafts = [Triangle("node_001", Still, 0, 5), Triangle("node_002", Still, 1, 6), Triangle("node_003", Still, 2, 5)];

        // Act
        MeshDraft mesh = Assert.Single(DraftMerger.Merge(drafts));

        // Assert
        Assert.Equal([5, 6, 5], mesh.Parts.Select(part => part.Material));
        Assert.Equal([6, 7, 8], mesh.Parts[2].Indices);
    }

    [Fact]
    public void Merge_AnotherOwnerInBetween_KeepsTheDrawOrder()
    {
        // Arrange
        List<MeshDraft> drafts = [Triangle("node_001", Still, 0, 5), Triangle("node_002", Glowing, 1, 5), Triangle("node_003", Still, 2, 5)];

        // Act
        List<MeshDraft> merged = DraftMerger.Merge(drafts);

        // Assert
        Assert.Equal(["node_001", "node_002", "node_003"], merged.Select(mesh => mesh.Name));
    }

    [Fact]
    public void Merge_AnotherRig_StaysApart()
    {
        // Arrange
        DraftOwner first = new(MuaBlend.Unset, MuaFlags.Animated, new SharedRig("rig_000", new RigKey(60, MuaBlend.Unset, MuaFlags.Animated)));
        DraftOwner second = new(MuaBlend.Unset, MuaFlags.Animated, new SharedRig("rig_001", new RigKey(60, MuaBlend.Unset, MuaFlags.Animated)));
        List<MeshDraft> drafts = [Triangle("node_001", first, 0, 5), Triangle("node_002", second, 1, 5), Triangle("node_003", second, 2, 5)];

        // Act
        List<MeshDraft> merged = DraftMerger.Merge(drafts);

        // Assert
        Assert.Equal(["node_001", "node_002"], merged.Select(mesh => mesh.Name));
        Assert.Same(second.Rig, merged[1].Owner.Rig);
    }

    [Fact]
    public void Merge_PulledTierDrawsFirst_JoinsAcrossItAndKeepsTheFirstOrder()
    {
        // Arrange
        List<MeshDraft> drafts = [Triangle("node_001", Still, 0, 5), Triangle("node_002", Still, 1, 5, Pulled), Triangle("node_003", Still, 2, 5)];

        // Act
        List<MeshDraft> merged = DraftMerger.Merge(drafts);

        // Assert
        Assert.Equal(["node_001", "node_002"], merged.Select(mesh => mesh.Name));
        Assert.Equal((0, Stage, 6), (merged[0].Order, merged[0].Tier, merged[0].Vertices.Count));
        Assert.Equal((1, Pulled), (merged[1].Order, merged[1].Tier));
    }

    [Fact]
    public void Merge_PastTheVertexLimit_StartsAnotherMesh()
    {
        // Arrange
        int half = MuaVertex.MostPerMesh / 2 + 1;
        List<MeshDraft> drafts = [Triangle("node_001", Still, 0, 5, Stage, half), Triangle("node_002", Still, 1, 5, Stage, half)];

        // Act
        List<MeshDraft> merged = DraftMerger.Merge(drafts);

        // Assert
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void Merge_Nothing_GivesNothing()
    {
        // Arrange
        List<MeshDraft> drafts = [];

        // Act
        List<MeshDraft> merged = DraftMerger.Merge(drafts);

        // Assert
        Assert.Empty(merged);
    }
}
