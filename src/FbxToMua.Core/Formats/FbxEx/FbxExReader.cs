using FbxToMua.Core.Binary;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.FbxEx;

public static class FbxExReader
{
    private const int HeaderBytes = 16;
    private const int BlockHeaderBytes = 8;
    private const int NodeHeaderBytes = 16;
    private const int Blocks = 4;

    private readonly record struct Block(int Body, int End, int Count);

    private sealed class Cursor(byte[] blob, int at, int end)
    {
        public int At { get; private set; } = at;

        public bool Done => At == end;

        public void Skip(int to) => At = to;

        private bool Has(long bytes) => At <= end && bytes <= end - At;

        public bool Int(out int value)
        {
            value = 0;

            if (!Has(4))
                return false;

            value = LittleEndian.I32(blob, At);
            At += 4;

            return true;
        }

        public bool Floats(long count, List<float> into)
        {
            if (count < 0 || count > (end - At) / 4)
                return false;

            for (long i = 0; i < count; ++i)
                into.Add(LittleEndian.F32(blob, At + i * 4));

            At += (int)count * 4;

            return true;
        }

        public bool Floats(Span<float> into)
        {
            if (into.Length > (end - At) / 4)
                return false;

            for (int i = 0; i < into.Length; ++i)
                into[i] = LittleEndian.F32(blob, At + i * 4);

            At += into.Length * 4;

            return true;
        }

        public bool Ints(long count, List<int> into)
        {
            if (count < 0 || count > (end - At) / 4)
                return false;

            for (long i = 0; i < count; ++i)
                into.Add(LittleEndian.I32(blob, At + i * 4));

            At += (int)count * 4;

            return true;
        }

        public bool Name(out string text)
        {
            text = string.Empty;

            if (!Has(FbxExWriter.NameBytes))
                return false;

            text = LittleEndian.Ascii(blob, At, FbxExWriter.NameBytes);
            At += FbxExWriter.NameBytes;

            return true;
        }
    }

    public static bool IsModel(byte[] blob) => blob.Length >= HeaderBytes && LittleEndian.Starts(blob, "fbxex\0\0\0");

    public static FbxExModel? Read(byte[] blob)
    {
        Block[]? blocks = BlocksOf(blob);

        if (blocks is null)
            return null;

        FbxExModel model = new();

        bool read = ReadTextures(blob, blocks[0], model) && ReadMaterials(blob, blocks[1], model)
            && ReadNodes(blob, blocks[2], model) && ReadAnimes(blob, blocks[3], model);

        return read ? model : null;
    }

    private static Block[]? BlocksOf(byte[] blob)
    {
        if (!IsModel(blob))
            return null;

        Block[] blocks = new Block[Blocks];
        long at = HeaderBytes;

        for (int i = 0; i < Blocks; ++i)
        {
            if (blob.Length - at < BlockHeaderBytes)
                return null;

            uint size = LittleEndian.U32(blob, at);
            int count = LittleEndian.I32(blob, at + 4);

            if (size < BlockHeaderBytes || size > blob.Length - at || count < 0)
                return null;

            blocks[i] = new Block((int)at + BlockHeaderBytes, (int)(at + size), count);
            at += size;
        }

        return at == blob.Length ? blocks : null;
    }

    private static bool ReadTextures(byte[] blob, Block block, FbxExModel model)
    {
        Cursor cursor = new(blob, block.Body, block.End);

        for (int i = 0; i < block.Count; ++i)
        {
            if (!cursor.Name(out string name))
                return false;

            model.Textures.Add(name);
        }

        return cursor.Done;
    }

    private static bool ReadMaterials(byte[] blob, Block block, FbxExModel model)
    {
        Cursor cursor = new(blob, block.Body, block.End);

        for (int i = 0; i < block.Count; ++i)
        {
            FbxExMaterial material = new();

            if (!cursor.Name(out string name) || !cursor.Int(out _) || !cursor.Int(out int texture)
                || !cursor.Floats(material.Value))
            {
                return false;
            }

            material.FileName = name;
            material.TextureIndex = texture;
            model.Materials.Add(material);
        }

        return cursor.Done;
    }

    private static bool ReadMesh(Cursor cursor, FbxExNode node)
    {
        Matrix matrix = new();

        if (!cursor.Int(out int blend) || !cursor.Int(out int alpha) || !cursor.Floats(matrix)
            || !cursor.Int(out int vertices) || vertices < 0
            || !cursor.Floats((long)vertices * FbxExNode.VertexFloats, node.Vertices))
        {
            return false;
        }

        node.BlendMode = blend;
        node.Alpha = alpha;
        node.Matrix = matrix;

        if (!cursor.Int(out int submeshes) || submeshes < 0)
            return false;

        for (int i = 0; i < submeshes; ++i)
        {
            FbxExSubmesh submesh = new();

            if (!cursor.Int(out int material) || !cursor.Int(out int indices) || indices < 0
                || !cursor.Ints(indices, submesh.Indices))
            {
                return false;
            }

            submesh.Material = material;
            node.Submeshes.Add(submesh);
        }

        return true;
    }

    private static bool ReadNodes(byte[] blob, Block block, FbxExModel model)
    {
        Cursor cursor = new(blob, block.Body, block.End);

        for (int i = 0; i < block.Count; ++i)
        {
            int start = cursor.At;

            if (!cursor.Int(out int size) || !cursor.Int(out int type) || !cursor.Int(out int child)
                || !cursor.Int(out int sibling) || size < NodeHeaderBytes || size > block.End - start)
            {
                return false;
            }

            FbxExNode node = new() { Type = type, Child = child, Sibling = sibling };
            Cursor payload = new(blob, start + NodeHeaderBytes, start + size);

            if (type == FbxExNode.MeshType && (!ReadMesh(payload, node) || !payload.Done))
                return false;

            model.Nodes.Add(node);
            cursor.Skip(start + size);
        }

        return cursor.Done;
    }

    private static bool ReadAnimes(byte[] blob, Block block, FbxExModel model)
    {
        Cursor cursor = new(blob, block.Body, block.End);

        for (int i = 0; i < block.Count; ++i)
        {
            List<float> track = [];

            if (!cursor.Int(out int frames) || frames < 0 || !cursor.Floats((long)frames * Matrix.Size, track))
                return false;

            model.Animes.Add(track);
        }

        return cursor.Done;
    }
}
