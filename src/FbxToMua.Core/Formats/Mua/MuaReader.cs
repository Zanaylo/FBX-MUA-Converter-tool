using FbxToMua.Core.Binary;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.Mua;

public readonly record struct MuaFlow(float Across, float Down, bool Known);

public readonly record struct MuaKey(Float4 Value, int Frame);

public readonly record struct MuaReadSkeleton(int FirstBone, int Bones, int Script, int Blend, uint Flags);

public struct MuaReadVertex
{
    public Float3 Position;
    public Float3 Normal;
    public float U;
    public float V;
    public MuaColour Colour;
    public int Bone;
}

public sealed class MuaReadMesh
{
    public string Name { get; init; } = string.Empty;
    public int Bone { get; init; }
    public int Skeleton { get; init; }
    public int Partner { get; init; }
    public int FirstVertex { get; init; }
    public int Vertices { get; init; }
    public int FirstPart { get; init; }
    public int Parts { get; init; }
    public Float3 Pivot;
    public bool Reversed { get; init; }
}

public sealed class MuaReadBone
{
    public string Name { get; init; } = string.Empty;
    public int Parent { get; init; }
    public Matrix Matrix;
    public Float3 Translation;
    public Float3 Rotation;
    public Float3 Scale;
    public int Frames { get; init; }
    public int[] Track { get; init; } = new int[4];
}

public sealed class MuaReader
{
    private const int Header = 0x20;
    private const int VertexBytes = 0x50;
    private const int MeshPivot = 0x88;
    private const int MeshPivotScale = 0xa0;
    private const int ReflectionLayer = 3;
    private const int BaseLayer = 1;
    private const int DeepestBoneChain = 256;

    private readonly byte[] _blob;
    private readonly uint[] _offset = new uint[MuaSection.Count];
    private readonly uint[] _count = new uint[MuaSection.Count];
    private readonly List<string> _strings = [];

    public List<string> Textures { get; } = [];
    public List<List<int>> Materials { get; } = [];
    public List<int> Reflections { get; } = [];
    public List<MuaFlow> Flows { get; } = [];
    public List<MuaReadBone> Bones { get; } = [];
    public List<MuaReadSkeleton> Skeletons { get; } = [];
    public List<string> Scripts { get; } = [];
    public List<MuaReadMesh> Meshes { get; } = [];
    public List<MuaPart> Parts { get; } = [];

    public bool HasGeometry => _count[MuaSection.Vertex] > 0 && _count[MuaSection.Index] > 0 && Textures.Count > 0;

    private MuaReader(byte[] blob)
    {
        _blob = blob;

        for (int i = 0; i < MuaSection.Count; ++i)
        {
            _offset[i] = LittleEndian.U32(blob, Header + i * 8);
            _count[i] = LittleEndian.U32(blob, Header + i * 8 + 4);
        }
    }

    public static bool IsModel(byte[] blob) => blob.Length >= Header + MuaSection.Count * 8 && LittleEndian.Starts(blob, "MUA\0");

    public static MuaReader? Read(byte[] blob)
    {
        if (!IsModel(blob))
            return null;

        MuaReader reader = new(blob);
        reader.ReadStrings();

        for (int i = 0; i < reader.Count(MuaSection.Script); ++i)
            reader.Scripts.Add(reader.Named(reader.Int(reader.Where(MuaSection.Script, i, 0x10))));

        reader.ReadTextures();
        reader.ReadMaterials();
        reader.ReadBones();
        reader.ReadMeshes();

        return reader.Meshes.Count == 0 ? null : reader;
    }

    public MuaReadVertex VertexAt(int index)
    {
        long at = _offset[MuaSection.Vertex] + (long)index * VertexBytes;
        MuaReadVertex vertex = new();

        for (int i = 0; i < 3; ++i)
        {
            vertex.Position[i] = Float(at + i * 4);
            vertex.Normal[i] = Float(at + 12 + i * 4);
        }

        vertex.U = Float(at + 36);
        vertex.V = Float(at + 40);

        if (at + 56 <= _blob.Length)
            vertex.Colour = new MuaColour(_blob[at + 54], _blob[at + 53], _blob[at + 52], _blob[at + 55]);

        vertex.Bone = (int)Float(at + 56);

        return vertex;
    }

    public List<MuaTriangle> Triangles(MuaPart part)
    {
        List<MuaTriangle> triangles = [];
        long at = _offset[MuaSection.Index] + (long)part.FirstIndex * 2;

        if (part.Indices < 3 || at + (long)part.Indices * 2 > _blob.Length)
            return triangles;

        for (int i = 0; i + 2 < part.Indices; ++i)
        {
            int a = LittleEndian.U16(_blob, at + i * 2);
            int b = LittleEndian.U16(_blob, at + (i + 1) * 2);
            int c = LittleEndian.U16(_blob, at + (i + 2) * 2);

            if (a == b || b == c || a == c)
                continue;

            triangles.Add(i % 2 != 0 ? new MuaTriangle(a, c, b) : new MuaTriangle(a, b, c));
        }

        return triangles;
    }

    public List<MuaKey> Keys(int track)
    {
        List<MuaKey> keys = [];

        if (track < 0 || track >= Count(MuaSection.AnimationList))
            return keys;

        long at = Where(MuaSection.AnimationList, track, 0x10);
        int first = Int(at);
        int count = Int(at + 4);

        for (int i = 0; i < count; ++i)
        {
            int index = first + i;

            if (index < 0 || index >= Count(MuaSection.AnimationKey))
                break;

            long row = Where(MuaSection.AnimationKey, index, 0x20);
            keys.Add(new MuaKey(new Float4(Float(row), Float(row + 4), Float(row + 8), Float(row + 12)), Int(row + 0x10)));
        }

        return keys;
    }

    public Matrix World(int bone)
    {
        Matrix world = Matrix.Identity;
        bool first = true;
        int index = bone;
        int guard = 0;

        while (index >= 0 && index < Bones.Count && guard++ < DeepestBoneChain)
        {
            world = first ? Bones[index].Matrix : MatrixMath.Multiply(world, Bones[index].Matrix);
            first = false;
            index = Bones[index].Parent;
        }

        return world;
    }

    private uint Dword(long at) => LittleEndian.U32(_blob, at);

    private int Int(long at) => (int)Dword(at);

    private float Float(long at) => LittleEndian.F32(_blob, at);

    private long Where(int section, int index, int stride) => _offset[section] + (long)index * stride;

    private int Count(int section) => (int)_count[section];

    private string Named(int index) => index >= 0 && index < _strings.Count ? _strings[index] : string.Empty;

    private void ReadStrings()
    {
        long baseAt = _offset[MuaSection.String];

        for (int i = 0; i < Count(MuaSection.StringInfo); ++i)
        {
            long at = Where(MuaSection.StringInfo, i, 0x10);
            _strings.Add(LittleEndian.Ascii(_blob, baseAt + Dword(at), (int)Dword(at + 4)));
        }
    }

    private void ReadTextures()
    {
        for (int i = 0; i < Count(MuaSection.Texture); ++i)
            Textures.Add(Named(Int(Where(MuaSection.Texture, i, 0x10))));
    }

    private void ReadMaterials()
    {
        List<int> assigned = [];
        List<int> layers = [];
        List<MuaFlow> flows = [];

        for (int i = 0; i < Count(MuaSection.Assign); ++i)
        {
            long at = Where(MuaSection.Assign, i, 0x20);
            layers.Add(Int(at));
            assigned.Add(Int(at + 4));
            flows.Add(FlowOf(Int(at + 8), Int(at + 12)));
        }

        for (int i = 0; i < Count(MuaSection.Material); ++i)
        {
            long at = Where(MuaSection.Material, i, 0x50);
            int count = Int(at);
            int first = Int(at + 4);
            List<int> taken = [];

            for (int k = 0; k < count; ++k)
            {
                int index = first + k;

                if (index >= 0 && index < assigned.Count)
                    taken.Add(index);
            }

            taken = [.. taken.Where(index => layers[index] == BaseLayer), .. taken.Where(index => layers[index] != BaseLayer)];

            List<int> textures = [];
            MuaFlow flow = default;
            int reflection = -1;

            foreach (int index in taken)
            {
                textures.Add(assigned[index]);

                if (reflection < 0 && layers[index] == ReflectionLayer)
                    reflection = assigned[index];

                if (!flow.Known && flows[index].Known)
                    flow = flows[index];
            }

            Materials.Add(textures);
            Reflections.Add(reflection);
            Flows.Add(flow);
        }
    }

    private MuaFlow FlowOf(int keys, int first)
    {
        if (keys < 2 || first < 0 || first + keys > Count(MuaSection.UvAnimation))
            return default;

        long one = Where(MuaSection.UvAnimation, first, 0x1c);
        long last = Where(MuaSection.UvAnimation, first + keys - 1, 0x1c);
        float span = (float)Dword(last + 0x10) - (float)Dword(one + 0x10);

        if (span <= 0.0f)
            return default;

        return new MuaFlow((Float(last) - Float(one)) / span, (Float(last + 4) - Float(one + 4)) / span, true);
    }

    private void ReadBones()
    {
        for (int i = 0; i < Count(MuaSection.Bone); ++i)
        {
            long at = Where(MuaSection.Bone, i, 0x130);
            MuaReadBone bone = new()
            {
                Name = Named(Int(at)),
                Parent = Int(at + 0x3c),
                Frames = Int(at + 0x108),
                Track = [Int(at + 0x10c), Int(at + 0x110), Int(at + 0x114), Int(at + 0x118)],
            };

            for (int k = 0; k < Matrix.Size; ++k)
                bone.Matrix[k] = Float(at + 0x48 + k * 4);

            for (int k = 0; k < 3; ++k)
            {
                bone.Translation[k] = Float(at + 8 + k * 4);
                bone.Rotation[k] = Float(at + 20 + k * 4);
                bone.Scale[k] = Float(at + 32 + k * 4);
            }

            Bones.Add(bone);
        }

        for (int i = 0; i < Count(MuaSection.Skeleton); ++i)
        {
            long at = Where(MuaSection.Skeleton, i, 0x20);
            Skeletons.Add(new MuaReadSkeleton(Int(at), Int(at + 4), Int(at + 8), Int(at + 0xc), Dword(at + 0x10)));
        }
    }

    private void ReadMeshes()
    {
        for (int i = 0; i < Count(MuaSection.Part); ++i)
        {
            long at = Where(MuaSection.Part, i, 0x20);
            Parts.Add(new MuaPart(Int(at), Int(at + 8), Int(at + 4)));
        }

        for (int i = 0; i < Count(MuaSection.Mesh); ++i)
        {
            long at = Where(MuaSection.Mesh, i, 0xc0);
            int skeleton = (int)Float(at);

            MuaReadMesh mesh = new()
            {
                Parts = Int(at + 8),
                FirstPart = Int(at + 12),
                Vertices = Int(at + 16),
                FirstVertex = Int(at + 20),
                Name = Named(Int(at + 0xb4)),
                Skeleton = skeleton,
                Partner = Int(at + 0xb8),
                Reversed = Float(at + MeshPivotScale) * Float(at + MeshPivotScale + 4) * Float(at + MeshPivotScale + 8) < 0.0f,
                Bone = skeleton >= 0 && skeleton < Skeletons.Count ? Skeletons[skeleton].FirstBone : -1,
            };

            for (int k = 0; k < 3; ++k)
                mesh.Pivot[k] = Float(at + MeshPivot + k * 4);

            Meshes.Add(mesh);
        }
    }
}
