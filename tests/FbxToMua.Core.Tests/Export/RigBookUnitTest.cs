using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Export;

public class RigBookUnitTest
{
    private static readonly RigKey Spinning = new(60, MuaBlend.Unset, MuaFlags.Animated);

    private static Limb Joint(int node, int parent) => new($"node_{node:D3}", parent, Pose.Rest, [Pose.Rest], new LimbOrigin(node, 0.0f));

    private static List<Limb> Chain(params int[] nodes) => nodes.Select((node, k) => Joint(node, k)).ToList();

    [Fact]
    public void Place_TwoNodesWithTheSameKey_ShareOneRig()
    {
        // Arrange
        RigBook book = new();

        // Act
        RigPlace first = book.Place(Spinning, Chain(4));
        RigPlace second = book.Place(Spinning, Chain(5));

        // Assert
        Assert.Same(first.Rig, second.Rig);
        Assert.Single(book.Rigs);
        Assert.Equal((1, 2), (first.Bone, second.Bone));
        Assert.Equal(3, first.Rig.Bones);
    }

    [Fact]
    public void Place_AnotherPeriod_OpensAnotherRig()
    {
        // Arrange
        RigBook book = new();

        // Act
        RigPlace first = book.Place(Spinning, Chain(4));
        RigPlace second = book.Place(Spinning with { Span = 90 }, Chain(5));

        // Assert
        Assert.NotSame(first.Rig, second.Rig);
        Assert.Equal(2, book.Rigs.Count);
        Assert.NotEqual(first.Rig.Name, second.Rig.Name);
    }

    [Fact]
    public void Place_SharedAncestor_AddsItOnce()
    {
        // Arrange
        RigBook book = new();

        // Act
        RigPlace left = book.Place(Spinning, Chain(2, 4));
        RigPlace right = book.Place(Spinning, Chain(2, 5));

        // Assert
        SharedRig rig = left.Rig;
        Assert.Equal(4, rig.Bones);
        Assert.Equal((2, 3), (left.Bone, right.Bone));
        Assert.Equal([0, 1, 1], rig.Limbs.Select(limb => limb.Parent));
    }

    [Fact]
    public void Place_SameNodeUnderAnotherPull_IsAnotherLimb()
    {
        // Arrange
        RigBook book = new();
        Limb pull = new("node_004_far", 0, Pose.Rest, [Pose.Rest], new LimbOrigin(LimbOrigin.NoNode, 0.5f));
        List<Limb> pulled = [pull, Joint(4, 1)];

        // Act
        RigPlace plain = book.Place(Spinning, Chain(4));
        RigPlace far = book.Place(Spinning, pulled);

        // Assert
        Assert.Same(plain.Rig, far.Rig);
        Assert.Equal(4, plain.Rig.Bones);
        Assert.Equal(3, far.Bone);
    }

    [Fact]
    public void Place_FullRig_OpensAnotherWithTheSameKey()
    {
        // Arrange
        RigBook book = new();

        for (int node = 0; node < RigBook.MostBones - 1; ++node)
            book.Place(Spinning, Chain(node));

        // Act
        RigPlace overflow = book.Place(Spinning, Chain(100));

        // Assert
        Assert.Equal(2, book.Rigs.Count);
        Assert.Equal(RigBook.MostBones, book.Rigs[0].Bones);
        Assert.Same(book.Rigs[1], overflow.Rig);
        Assert.Equal(1, overflow.Bone);
    }

    [Fact]
    public void Place_ChainLongerThanARig_GetsARigOfItsOwn()
    {
        // Arrange
        RigBook book = new();
        int[] deep = Enumerable.Range(0, RigBook.MostBones + 3).ToArray();
        book.Place(Spinning, Chain(200));

        // Act
        RigPlace placed = book.Place(Spinning, Chain(deep));

        // Assert
        Assert.Equal(2, book.Rigs.Count);
        Assert.Same(book.Rigs[1], placed.Rig);
        Assert.Equal(deep.Length, placed.Bone);
    }

    [Fact]
    public void Place_AnAncestorAlreadyThere_FitsWhenOnlyTheNewLimbsFit()
    {
        // Arrange
        RigBook book = new();
        book.Place(Spinning, Chain(1, 2));

        for (int node = 10; node < 10 + RigBook.MostBones - 4; ++node)
            book.Place(Spinning, Chain(node));

        // Act
        RigPlace placed = book.Place(Spinning, Chain(1, 3));

        // Assert
        Assert.Single(book.Rigs);
        Assert.Equal(RigBook.MostBones, placed.Rig.Bones);
    }
}
