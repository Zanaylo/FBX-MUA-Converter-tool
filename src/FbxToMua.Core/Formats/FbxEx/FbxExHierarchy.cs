using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.FbxEx;

public sealed class FbxExHierarchy
{
    private const int None = -1;
    private const float Still = 1e-6f;

    private readonly FbxExModel _model;
    private readonly int[] _parent;
    private readonly bool[] _varies;

    public FbxExHierarchy(FbxExModel model)
    {
        _model = model;
        int count = model.Nodes.Count;
        _parent = Enumerable.Repeat(None, count).ToArray();
        _varies = new bool[count];

        for (int i = 0; i < count; ++i)
        {
            int guard = 0;

            for (int child = model.Nodes[i].Child; child >= 0 && child < count && guard++ < count;
                child = model.Nodes[child].Sibling)
            {
                _parent[child] = i;
            }
        }

        for (int i = 0; i < count && i < model.Animes.Count; ++i)
            _varies[i] = Moves(model.Animes[i]);
    }

    public int Parent(int node) => node >= 0 && node < _parent.Length ? _parent[node] : None;

    public int Frames(int node) => node >= 0 && node < _model.Animes.Count ? _model.Animes[node].Count / Matrix.Size : 0;

    public bool Varies(int node) => node >= 0 && node < _varies.Length && _varies[node];

    public Matrix Local(int node, int frame)
    {
        int frames = Frames(node);

        if (frames == 0)
            return Matrix.Identity;

        int at = frame % frames * Matrix.Size;

        return Matrix.From(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_model.Animes[node]).Slice(at, Matrix.Size));
    }

    public Matrix World(int node, int frame)
    {
        Matrix world = Local(node, frame);
        int guard = 0;

        for (int parent = Parent(node); parent >= 0 && guard++ < _parent.Length; parent = Parent(parent))
            world = MatrixMath.Multiply(world, Local(parent, frame));

        return world;
    }

    private static bool Moves(List<float> track)
    {
        for (int at = Matrix.Size; at + Matrix.Size <= track.Count; at += Matrix.Size)
        {
            for (int k = 0; k < Matrix.Size; ++k)
            {
                if (MathF.Abs(track[at + k] - track[k]) > Still)
                    return true;
            }
        }

        return false;
    }
}
