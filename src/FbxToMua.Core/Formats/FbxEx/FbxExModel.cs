using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.FbxEx;

public sealed class FbxExSubmesh
{
    public int Material { get; set; }
    public List<int> Indices { get; set; } = [];

    public FbxExSubmesh Clone() => new() { Material = Material, Indices = [.. Indices] };

    public static List<FbxExSubmesh> Clone(IEnumerable<FbxExSubmesh> submeshes) => submeshes.Select(submesh => submesh.Clone()).ToList();
}

public sealed class FbxExNode
{
    public const int BranchType = 0;
    public const int MeshType = 1;
    public const int VertexFloats = 12;
    public const int PositionAt = 0;
    public const int NormalAt = 3;
    public const int ColourAt = 6;
    public const int UvAt = 10;

    public int Type { get; set; }
    public int Child { get; set; } = -1;
    public int Sibling { get; set; } = -1;
    public int BlendMode { get; set; }
    public int Alpha { get; set; }
    public Matrix Matrix = Matrix.Identity;
    public List<float> Vertices { get; set; } = [];
    public List<FbxExSubmesh> Submeshes { get; set; } = [];

    public int VertexCount => Vertices.Count / VertexFloats;

    public FbxExNode Clone()
    {
        return new FbxExNode
        {
            Type = Type,
            Child = Child,
            Sibling = Sibling,
            BlendMode = BlendMode,
            Alpha = Alpha,
            Matrix = Matrix,
            Vertices = [.. Vertices],
            Submeshes = FbxExSubmesh.Clone(Submeshes),
        };
    }

    public static FbxExNode Branch() => new() { Type = BranchType };

    public static FbxExNode Leaf() => new() { Type = MeshType };
}

public sealed class FbxExMaterial
{
    public const int Values = 17;

    public string FileName { get; set; } = string.Empty;
    public int TextureIndex { get; set; }
    public float[] Value { get; set; } = new float[Values];
}

public sealed class FbxExModel
{
    public List<string> Textures { get; set; } = [];
    public List<FbxExMaterial> Materials { get; set; } = [];
    public List<FbxExNode> Nodes { get; set; } = [];
    public List<List<float>> Animes { get; set; } = [];
}
