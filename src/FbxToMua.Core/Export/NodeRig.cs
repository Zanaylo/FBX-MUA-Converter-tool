using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public sealed record RigJoint(int Node, List<Pose> Poses);

public sealed record Rig(int Span, List<RigJoint> Joints);

public static class NodeRig
{
    public const int LongestTake = 12000;

    public static Rig? Of(FbxExHierarchy scene, int node, Matrix place)
    {
        List<int> chain = Chain(scene, node);

        if (chain.Count == 0)
            return null;

        Matrix unplace = MatrixMath.Invert(place);
        int parent = scene.Parent(chain[0]);
        Matrix above = parent >= 0 ? scene.World(parent, 0) : Matrix.Identity;
        int span = Period(scene, chain);
        List<RigJoint> joints = [];

        for (int k = 0; k < chain.Count; ++k)
        {
            List<Pose> poses = [];

            for (int frame = 0; frame <= span; ++frame)
            {
                Matrix local = scene.Local(chain[k], frame % span);

                if (k == 0)
                    local = MatrixMath.Multiply(local, above);

                Pose pose = PoseMath.Split(MatrixMath.Multiply(MatrixMath.Multiply(unplace, local), place));

                if (poses.Count > 0 && poses[^1].Turn.Dot(pose.Turn) < 0.0f)
                    pose.Turn = pose.Turn.Negated();

                poses.Add(pose);
            }

            joints.Add(new RigJoint(chain[k], poses));
        }

        return new Rig(span, joints);
    }

    private static List<int> Chain(FbxExHierarchy scene, int node)
    {
        List<int> upward = [];
        int top = -1;

        for (int at = node; at >= 0; at = scene.Parent(at))
        {
            upward.Add(at);

            if (scene.Varies(at))
                top = upward.Count;
        }

        if (top < 0)
            return [];

        upward.RemoveRange(top, upward.Count - top);
        upward.Reverse();

        return upward;
    }

    private static int Period(FbxExHierarchy scene, List<int> chain)
    {
        long period = 1;
        int longest = 1;

        foreach (int node in chain)
        {
            if (!scene.Varies(node))
                continue;

            int frames = Math.Max(1, scene.Frames(node));
            longest = Math.Max(longest, frames);
            period = period / Common(period, frames) * frames;

            if (period > LongestTake)
                return longest;
        }

        return (int)period;
    }

    private static long Common(long left, long right)
    {
        while (right != 0)
        {
            long rest = left % right;
            left = right;
            right = rest;
        }

        return left;
    }
}
