using FbxToMua.Core.Binary;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.FbxEx;

public static class FbxExLocal
{
    private const int HeaderBytes = 16;
    private const int BlockHeaderBytes = 8;
    private const int BlockCount = 4;
    private const int NodeBlock = 2;
    private const int AnimationBlock = 3;
    private const int RecordHeaderBytes = 16;
    private const int WorldOffset = RecordHeaderBytes + 8;
    private const int MatrixBytes = Matrix.Size * 4;
    private const int FrameCountBytes = 4;
    private const float Epsilon = 0.001f;
    private const float Singular = 1e-12f;

    private sealed class Node
    {
        public int Child { get; init; }
        public int Sibling { get; init; }
        public int Parent { get; set; } = -1;
        public bool Meshed { get; init; }
        public float[] World { get; init; } = [];
    }

    private readonly record struct Track(int At, int Frames);

    private readonly record struct Block(int At, int End, int Count);

    public readonly record struct Report(int Nodes, int Frames);

    public static Report? Apply(byte[] data)
    {
        if (!TryParse(data, out List<Node> nodes, out List<Track> tracks) || !Baked(data, nodes, tracks))
            return null;

        return Rebase(data, nodes, tracks);
    }

    private static float[] ReadMatrix(byte[] data, int at)
    {
        float[] matrix = new float[Matrix.Size];

        for (int i = 0; i < Matrix.Size; ++i)
            matrix[i] = LittleEndian.F32(data, at + i * 4);

        return matrix;
    }

    private static void WriteMatrix(byte[] data, int at, float[] value)
    {
        for (int i = 0; i < Matrix.Size; ++i)
            BitConverter.TryWriteBytes(data.AsSpan(at + i * 4), value[i]);
    }

    private static float[] Multiply(float[] left, float[] right)
    {
        float[] product = new float[Matrix.Size];

        for (int column = 0; column < 4; ++column)
        {
            for (int row = 0; row < 4; ++row)
            {
                product[column * 4 + row] = left[row] * right[column * 4] + left[4 + row] * right[column * 4 + 1]
                    + left[8 + row] * right[column * 4 + 2] + left[12 + row] * right[column * 4 + 3];
            }
        }

        return product;
    }

    private static float[]? Inverse(float[] value)
    {
        float[,] a =
        {
            { value[0], value[4], value[8] },
            { value[1], value[5], value[9] },
            { value[2], value[6], value[10] },
        };

        float determinant = a[0, 0] * (a[1, 1] * a[2, 2] - a[1, 2] * a[2, 1])
            - a[0, 1] * (a[1, 0] * a[2, 2] - a[1, 2] * a[2, 0])
            + a[0, 2] * (a[1, 0] * a[2, 1] - a[1, 1] * a[2, 0]);

        if (MathF.Abs(determinant) < Singular)
            return null;

        float[,] b = new float[3, 3];
        b[0, 0] = (a[1, 1] * a[2, 2] - a[1, 2] * a[2, 1]) / determinant;
        b[0, 1] = (a[0, 2] * a[2, 1] - a[0, 1] * a[2, 2]) / determinant;
        b[0, 2] = (a[0, 1] * a[1, 2] - a[0, 2] * a[1, 1]) / determinant;
        b[1, 0] = (a[1, 2] * a[2, 0] - a[1, 0] * a[2, 2]) / determinant;
        b[1, 1] = (a[0, 0] * a[2, 2] - a[0, 2] * a[2, 0]) / determinant;
        b[1, 2] = (a[0, 2] * a[1, 0] - a[0, 0] * a[1, 2]) / determinant;
        b[2, 0] = (a[1, 0] * a[2, 1] - a[1, 1] * a[2, 0]) / determinant;
        b[2, 1] = (a[0, 1] * a[2, 0] - a[0, 0] * a[2, 1]) / determinant;
        b[2, 2] = (a[0, 0] * a[1, 1] - a[0, 1] * a[1, 0]) / determinant;

        float[] inverse = new float[Matrix.Size];

        for (int column = 0; column < 3; ++column)
        {
            for (int row = 0; row < 3; ++row)
                inverse[column * 4 + row] = b[row, column];
        }

        for (int row = 0; row < 3; ++row)
            inverse[12 + row] = -(b[row, 0] * value[12] + b[row, 1] * value[13] + b[row, 2] * value[14]);

        inverse[15] = 1.0f;

        return inverse;
    }

    private static bool Same(float[] left, float[] right)
    {
        for (int i = 0; i < Matrix.Size; ++i)
        {
            if (MathF.Abs(left[i] - right[i]) > Epsilon)
                return false;
        }

        return true;
    }

    private static Block[]? Blocks(byte[] data)
    {
        if (data.Length < HeaderBytes || !LittleEndian.Starts(data, "fbxex"))
            return null;

        Block[] blocks = new Block[BlockCount];
        int at = HeaderBytes;

        for (int i = 0; i < BlockCount; ++i)
        {
            if (at + BlockHeaderBytes > data.Length)
                return null;

            uint size = LittleEndian.U32(data, at);

            if (size < BlockHeaderBytes || size > data.Length - at)
                return null;

            blocks[i] = new Block(at, at + (int)size, LittleEndian.I32(data, at + 4));
            at += (int)size;
        }

        return blocks;
    }

    private static bool TryParse(byte[] data, out List<Node> nodes, out List<Track> tracks)
    {
        nodes = [];
        tracks = [];
        Block[]? blocks = Blocks(data);

        return blocks is not null && TakeNodes(data, blocks[NodeBlock], nodes) && TakeParents(nodes)
            && TakeTracks(data, blocks[AnimationBlock], nodes.Count, tracks);
    }

    private static bool TakeNodes(byte[] data, Block block, List<Node> nodes)
    {
        if (block.Count <= 0)
            return false;

        int at = block.At + BlockHeaderBytes;

        for (int i = 0; i < block.Count; ++i)
        {
            if (at + RecordHeaderBytes > block.End)
                return false;

            int size = LittleEndian.I32(data, at);

            if (size < RecordHeaderBytes || size > block.End - at)
                return false;

            bool meshed = LittleEndian.I32(data, at + 4) == FbxExNode.MeshType;

            if (meshed && size < WorldOffset + MatrixBytes)
                return false;

            nodes.Add(new Node
            {
                Child = LittleEndian.I32(data, at + 8),
                Sibling = LittleEndian.I32(data, at + 12),
                Meshed = meshed,
                World = meshed ? ReadMatrix(data, at + WorldOffset) : [],
            });

            at += size;
        }

        return true;
    }

    private static bool TakeParents(List<Node> nodes)
    {
        for (int i = 0; i < nodes.Count; ++i)
        {
            for (int child = nodes[i].Child; child >= 0; child = nodes[child].Sibling)
            {
                if (child >= nodes.Count || nodes[child].Parent >= 0)
                    return false;

                nodes[child].Parent = i;
            }
        }

        return true;
    }

    private static bool TakeTracks(byte[] data, Block block, int nodes, List<Track> tracks)
    {
        if (block.Count < 0 || block.Count != nodes)
            return false;

        int at = block.At + BlockHeaderBytes;

        for (int i = 0; i < block.Count; ++i)
        {
            if (at + FrameCountBytes > block.End)
                return false;

            int frames = LittleEndian.I32(data, at);
            at += FrameCountBytes;

            if (frames <= 0 || frames > (block.End - at) / MatrixBytes)
                return false;

            tracks.Add(new Track(at, frames));
            at += frames * MatrixBytes;
        }

        return true;
    }

    private static bool Baked(byte[] data, List<Node> nodes, List<Track> tracks)
    {
        int meshes = 0;

        for (int i = 0; i < nodes.Count; ++i)
        {
            if (!nodes[i].Meshed)
                continue;

            ++meshes;

            if (!Same(ReadMatrix(data, tracks[i].At), nodes[i].World))
                return false;
        }

        return meshes != 0;
    }

    private static Report Rebase(byte[] data, List<Node> nodes, List<Track> tracks)
    {
        byte[] source = (byte[])data.Clone();
        int movedNodes = 0;
        int movedFrames = 0;

        for (int i = 0; i < nodes.Count; ++i)
        {
            int parent = nodes[i].Parent;

            if (parent <= 0)
                continue;

            Track track = tracks[i];
            Track above = tracks[parent];
            bool moved = false;

            for (int frame = 0; frame < track.Frames; ++frame)
            {
                int taken = frame < above.Frames ? frame : above.Frames - 1;
                float[]? inverse = Inverse(ReadMatrix(source, above.At + taken * MatrixBytes));

                if (inverse is null)
                    continue;

                int at = track.At + frame * MatrixBytes;
                float[] world = ReadMatrix(source, at);
                float[] local = Multiply(inverse, world);

                if (Same(local, world))
                    continue;

                WriteMatrix(data, at, local);
                ++movedFrames;
                moved = true;
            }

            if (moved)
                ++movedNodes;
        }

        return new Report(movedNodes, movedFrames);
    }
}
