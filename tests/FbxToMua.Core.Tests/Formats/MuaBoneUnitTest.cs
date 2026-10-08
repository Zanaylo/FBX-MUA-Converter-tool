using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Formats;

public class MuaBoneUnitTest
{
    private static Pose Lifted()
    {
        Pose lifted = new() { Translation = new Float3(0.0f, 3.0f, 0.0f), Scale = Float3.All(2.0f) };

        return PoseMath.Turned(lifted);
    }

    private static Pose Aside()
    {
        Pose aside = new() { Translation = new Float3(1.0f, 0.0f, 0.0f), Scale = Float3.All(1.0f) };

        return PoseMath.Turned(aside);
    }

    private static readonly int[] LayerParents = [-1, 0, 1, 1];

    private static List<MuaBone> LayerBones()
    {
        return [MuaBone.Root("Bone_setting"), MuaBone.Root("Bone_layer"),
            MuaBone.Joint("frame", Lifted()), MuaBone.Joint("a", Aside()), MuaBone.Joint("b", Aside())];
    }

    [Fact]
    public void Tree_LayerSkeleton_LinksParentsChildrenAndSiblings()
    {
        // Arrange
        List<MuaBone> bones = LayerBones();

        // Act
        MuaBone.Tree(bones, 1, LayerParents);

        // Assert
        Assert.Equal((-1, -1), (bones[0].Parent, bones[0].Child));
        Assert.Equal((-1, 1, -1), (bones[1].Parent, bones[1].Child, bones[1].Sibling));
        Assert.Equal((0, 2, -1), (bones[2].Parent, bones[2].Child, bones[2].Sibling));
        Assert.Equal((1, 3, -1), (bones[3].Parent, bones[3].Sibling, bones[3].Child));
        Assert.Equal((1, -1), (bones[4].Parent, bones[4].Sibling));
        Assert.Equal((2, 3), (bones[3].Index, bones[4].Index));
    }

    [Fact]
    public void Tree_LayerSkeleton_UnbindsUndoTheWorld()
    {
        // Arrange
        List<MuaBone> bones = LayerBones();

        // Act
        MuaBone.Tree(bones, 1, LayerParents);

        // Assert
        Matrix world = MatrixMath.Multiply(bones[4].Matrix, bones[2].Matrix);
        Assert.True(Tolerance.Near(Matrix.Identity, MatrixMath.Multiply(world, bones[4].Unbind), 1e-5f));
        Assert.True(Tolerance.Near(MatrixMath.Invert(bones[2].Matrix), bones[4].ParentUnbind, 1e-5f));
    }

    [Fact]
    public void Link_Chain_IsATreeWithOneChildEach()
    {
        // Arrange
        List<MuaBone> chain = [MuaBone.Root("r"), MuaBone.Joint("a", Aside()), MuaBone.Joint("b", Aside())];
        List<MuaBone> linked = [MuaBone.Root("r"), MuaBone.Joint("a", Aside()), MuaBone.Joint("b", Aside())];

        // Act
        MuaBone.Link(linked, 0, 3);
        MuaBone.Tree(chain, 0, [-1, 0, 1]);

        // Assert
        for (int i = 0; i < chain.Count; ++i)
        {
            Assert.Equal(linked[i].Parent, chain[i].Parent);
            Assert.Equal(linked[i].Child, chain[i].Child);
            Assert.True(Tolerance.Near(linked[i].Unbind, chain[i].Unbind, 1e-6f));
        }
    }
}
