using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.Mua;

public sealed class MuaBone
{
    public const int RootKind = 5;
    public const int MeshKind = 0x84;
    public const int JointKind = 1;
    public const int None = -1;

    public string Name { get; set; } = string.Empty;
    public int Kind { get; set; }
    public int Parent { get; set; }
    public int Child { get; set; }
    public int Sibling { get; set; }
    public int Index { get; set; }
    public Pose Pose;
    public Matrix Matrix;
    public Matrix Unbind;
    public Matrix ParentUnbind;

    public static MuaBone Root(string name)
    {
        return new MuaBone
        {
            Name = name,
            Kind = RootKind,
            Parent = None,
            Child = None,
            Sibling = None,
            Pose = Pose.Rest,
            Matrix = Matrix.Identity,
            Unbind = Matrix.Identity,
            ParentUnbind = Matrix.Identity,
        };
    }

    public static MuaBone Joint(string name, Pose pose)
    {
        MuaBone joint = Root(name);
        joint.Kind = JointKind;
        joint.Pose = pose;
        joint.Matrix = PoseMath.Compose(pose);

        return joint;
    }

    public static void Link(List<MuaBone> bones, int first, int count)
    {
        Tree(bones, first, Enumerable.Range(0, count).Select(i => i - 1).ToList());
    }

    public static void Tree(List<MuaBone> bones, int first, IReadOnlyList<int> parents)
    {
        int count = parents.Count;

        if (first < 0 || count < 1 || first + count > bones.Count)
            return;

        Matrix[] worlds = new Matrix[count];

        for (int i = 0; i < count; ++i)
        {
            MuaBone bone = bones[first + i];
            bone.Index = i;
            bone.Parent = parents[i] < i ? parents[i] : None;
            bone.Child = None;
            bone.Sibling = None;

            Matrix parentWorld = bone.Parent == None ? Matrix.Identity : worlds[bone.Parent];
            Matrix world = MatrixMath.Multiply(bone.Matrix, parentWorld);
            bone.Unbind = MatrixMath.Invert(world);
            bone.ParentUnbind = MatrixMath.Invert(parentWorld);
            worlds[i] = world;
        }

        for (int i = count - 1; i > 0; --i)
        {
            int parent = bones[first + i].Parent;

            if (parent == None)
                continue;

            bones[first + i].Sibling = bones[first + parent].Child;
            bones[first + parent].Child = i;
        }
    }
}
