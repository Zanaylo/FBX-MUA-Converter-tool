using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Formats.FbxEx;

public static class FbxExWriter
{
    public const int NameBytes = 128;
    private const int NodePrefix = 16;

    public static byte[] Build(FbxExModel model)
    {
        ByteSink sink = new();
        sink.Text("fbxex\0\0\0");
        sink.Dword(0);
        sink.Dword(0);

        ByteSink body = new();

        foreach (string name in model.Textures)
            PutName(body, name);

        PutBlock(sink, model.Textures.Count, body);

        body = new ByteSink();

        for (int i = 0; i < model.Materials.Count; ++i)
        {
            PutName(body, model.Materials[i].FileName);
            body.Int(i);
            body.Int(model.Materials[i].TextureIndex);
            body.Floats(model.Materials[i].Value);
        }

        PutBlock(sink, model.Materials.Count, body);

        body = new ByteSink();

        foreach (FbxExNode node in model.Nodes)
        {
            ByteSink payload = new();

            if (node.Type == FbxExNode.MeshType)
                PutMesh(payload, node);

            body.Int(payload.Size + NodePrefix);
            body.Int(node.Type);
            body.Int(node.Child);
            body.Int(node.Sibling);
            body.Bytes(payload.ToArray());
        }

        PutBlock(sink, model.Nodes.Count, body);

        body = new ByteSink();

        foreach (List<float> track in model.Animes)
        {
            body.Int(track.Count / Geometry.Matrix.Size);
            body.Floats(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(track));
        }

        PutBlock(sink, model.Animes.Count, body);

        return sink.ToArray();
    }

    private static void PutName(ByteSink sink, string text)
    {
        byte[] bytes = LittleEndian.Latin1(text);

        for (int i = 0; i < NameBytes; ++i)
            sink.Byte(i < bytes.Length ? bytes[i] : (byte)0);
    }

    private static void PutBlock(ByteSink sink, int count, ByteSink body)
    {
        sink.Int(body.Size + 8);
        sink.Int(count);
        sink.Bytes(body.ToArray());
    }

    private static void PutMesh(ByteSink sink, FbxExNode node)
    {
        sink.Int(node.BlendMode);
        sink.Int(node.Alpha);
        sink.Floats(node.Matrix);
        sink.Int(node.VertexCount);
        sink.Floats(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(node.Vertices));
        sink.Int(node.Submeshes.Count);

        foreach (FbxExSubmesh submesh in node.Submeshes)
        {
            sink.Int(submesh.Material);
            sink.Int(submesh.Indices.Count);

            foreach (int index in submesh.Indices)
                sink.Int(index);
        }
    }
}
