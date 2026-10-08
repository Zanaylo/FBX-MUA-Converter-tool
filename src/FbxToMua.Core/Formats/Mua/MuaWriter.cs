using FbxToMua.Core.Binary;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.Mua;

public static class MuaWriter
{
    private const uint Version = 0x3ee;
    private const int Table = 0x20;
    private const int Data = Table + MuaSection.Count * 8;
    private const int BaseLayer = 1;
    private const float Ambient = 0.588f;
    private const float Diffuse = 0.588f;
    private const float Specular = 0.9f;
    private const float Power = 0.1f;
    private const float Shown = 1.0f;
    private const int Corners = 8;

    private sealed class Strings
    {
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

        public List<string> All { get; } = [];

        public int Of(string text)
        {
            if (_index.TryGetValue(text, out int known))
                return known;

            _index[text] = All.Count;
            All.Add(text);

            return All.Count - 1;
        }
    }

    private struct Bounds
    {
        public Float3 Centre;
        public float Radius;
        public Float3 Low;
        public Float3 High;
    }

    public static byte[] Build(MuaModel model, bool withGeometry)
    {
        Strings strings = new();
        Intern(model, strings);

        uint[] counts =
        [
            (uint)model.Skeletons.Count, (uint)model.Bones.Count, (uint)model.Meshes.Count, (uint)model.Parts.Count,
            (uint)model.Materials.Count, (uint)model.Materials.Count, (uint)model.Textures.Count, 0, 0, 0, 0,
            (uint)model.Scripts.Count, (uint)model.Parts.Count,
            withGeometry ? (uint)model.Vertices.Count : 0, withGeometry ? (uint)model.Indices.Count : 0,
            (uint)strings.All.Count, 0,
        ];

        ByteSink body = new();
        uint[] offsets = new uint[MuaSection.Count];

        void Mark(int section) => offsets[section] = (uint)(Data + body.Size);

        Mark(MuaSection.Skeleton);
        PutSkeletons(body, model);
        Mark(MuaSection.Bone);
        PutBones(body, model, strings);
        Mark(MuaSection.Mesh);
        PutMeshes(body, model, strings);
        Mark(MuaSection.Part);
        PutParts(body, model);
        Mark(MuaSection.Material);
        PutMaterials(body, model);
        Mark(MuaSection.Assign);
        PutAssigns(body, model);
        Mark(MuaSection.Texture);
        PutNames(body, model.Textures, strings, MuaSection.Stride[MuaSection.Texture]);

        for (int empty = MuaSection.Texture + 1; empty < MuaSection.Script; ++empty)
            Mark(empty);

        Mark(MuaSection.Script);
        PutNames(body, model.Scripts, strings, MuaSection.Stride[MuaSection.Script]);
        Mark(MuaSection.Order);
        PutOrder(body, model);
        Mark(MuaSection.Vertex);

        if (withGeometry)
            PutVertices(body, model);

        Mark(MuaSection.Index);

        if (withGeometry)
        {
            foreach (ushort index in model.Indices)
                body.Word(index);
        }

        Mark(MuaSection.StringInfo);
        PutStringInfo(body, strings);
        Mark(MuaSection.String);
        PutStrings(body, strings);

        uint stringBytes = (uint)strings.All.Sum(text => text.Length + 1);

        ByteSink head = new();
        head.Text("MUA\0");
        head.Dword(Version);
        head.Dword(MuaSection.Count);
        head.PadTo(Table);

        for (int i = 0; i < MuaSection.Count; ++i)
        {
            head.Dword(offsets[i]);
            head.Dword(i == MuaSection.String ? stringBytes : counts[i]);
        }

        head.Bytes(body.ToArray());

        return head.ToArray();
    }

    private static void Intern(MuaModel model, Strings strings)
    {
        foreach (MuaBone bone in model.Bones)
            strings.Of(bone.Name);

        foreach (MuaMesh mesh in model.Meshes)
            strings.Of(mesh.Name);

        foreach (string texture in model.Textures)
            strings.Of(texture);

        foreach (string script in model.Scripts)
            strings.Of(script);
    }

    private static void PutSkeletons(ByteSink sink, MuaModel model)
    {
        foreach (MuaSkeleton skeleton in model.Skeletons)
        {
            int start = sink.Size;
            sink.Int(skeleton.FirstBone);
            sink.Int(skeleton.Bones);
            sink.Int(skeleton.Script);
            sink.Dword((uint)skeleton.Blend);
            sink.Dword(skeleton.Flags);
            sink.Pad(start, MuaSection.Stride[MuaSection.Skeleton]);
        }
    }

    private static void PutBones(ByteSink sink, MuaModel model, Strings strings)
    {
        foreach (MuaBone bone in model.Bones)
        {
            int start = sink.Size;
            bool joint = bone.Kind == MuaBone.JointKind;

            sink.Int(strings.Of(bone.Name));
            sink.Int(bone.Kind);
            sink.Floats(bone.Pose.Translation);
            sink.Floats(bone.Pose.Rotation);
            sink.Floats(bone.Pose.Scale);
            sink.Int(0);
            sink.Int(joint ? 1 : 0);
            sink.Int(0);
            sink.Int(joint ? bone.Index : 0);
            sink.Int(bone.Parent);
            sink.Int(bone.Child);
            sink.Int(bone.Sibling);
            sink.Floats(bone.Matrix);
            sink.Floats(bone.Unbind);
            sink.Floats(bone.ParentUnbind);
            sink.Pad(start, MuaSection.Stride[MuaSection.Bone]);
        }
    }

    private static Bounds BoundsOf(MuaModel model, MuaMesh mesh)
    {
        Bounds bounds = new();

        if (mesh.Vertices <= 0)
            return bounds;

        bounds.Low = model.Vertices[mesh.FirstVertex].Position;
        bounds.High = bounds.Low;

        for (int v = 0; v < mesh.Vertices; ++v)
        {
            Float3 position = model.Vertices[mesh.FirstVertex + v].Position;

            for (int k = 0; k < 3; ++k)
            {
                bounds.Low[k] = Std.Min(bounds.Low[k], position[k]);
                bounds.High[k] = Std.Max(bounds.High[k], position[k]);
            }
        }

        for (int k = 0; k < 3; ++k)
            bounds.Centre[k] = (bounds.Low[k] + bounds.High[k]) * 0.5f;

        float farthest = 0.0f;

        for (int v = 0; v < mesh.Vertices; ++v)
        {
            Float3 position = model.Vertices[mesh.FirstVertex + v].Position;
            float distance = 0.0f;

            for (int k = 0; k < 3; ++k)
                distance += (position[k] - bounds.Centre[k]) * (position[k] - bounds.Centre[k]);

            farthest = Std.Max(farthest, distance);
        }

        bounds.Radius = MathF.Sqrt(farthest);

        return bounds;
    }

    private static bool CornerIsHigh(int axis, int corner)
    {
        return axis switch
        {
            0 => (corner + 1) / 2 % 2 == 0,
            1 => corner % 4 < 2,
            _ => corner < 4,
        };
    }

    private static void PutMeshes(ByteSink sink, MuaModel model, Strings strings)
    {
        foreach (MuaMesh mesh in model.Meshes)
        {
            int start = sink.Size;
            Bounds bounds = BoundsOf(model, mesh);

            sink.Float(mesh.Skeleton);
            sink.Int(0);
            sink.Int(mesh.Parts);
            sink.Int(mesh.FirstPart);
            sink.Int(mesh.Vertices);
            sink.Int(mesh.FirstVertex);
            sink.Floats(bounds.Centre);
            sink.Float(bounds.Radius);

            for (int axis = 0; axis < 3; ++axis)
            {
                for (int corner = 0; corner < Corners; ++corner)
                    sink.Float(CornerIsHigh(axis, corner) ? bounds.High[axis] : bounds.Low[axis]);
            }

            sink.Floats(mesh.Pivot);
            sink.Floats(new Float3());
            sink.Floats(Float3.All(1.0f));
            sink.Float(Shown);
            sink.Int(0);
            sink.Int(strings.Of(mesh.Name));
            sink.Int(mesh.Partner);
            sink.Pad(start, MuaSection.Stride[MuaSection.Mesh]);
        }
    }

    private static void PutParts(ByteSink sink, MuaModel model)
    {
        foreach (MuaPart part in model.Parts)
            sink.Record(MuaSection.Stride[MuaSection.Part], (uint)part.Material, (uint)part.Indices, (uint)part.FirstIndex);
    }

    private static void PutMaterials(ByteSink sink, MuaModel model)
    {
        for (int i = 0; i < model.Materials.Count; ++i)
        {
            int start = sink.Size;
            sink.Int(1);
            sink.Int(i);
            sink.Int(0);
            sink.Floats(Float3.All(Ambient));
            sink.Int(0);
            sink.Floats(Float3.All(Diffuse));
            sink.Int(0);
            sink.Floats(Float3.All(Specular));
            sink.Float(Power);
            sink.Pad(start, MuaSection.Stride[MuaSection.Material]);
        }
    }

    private static void PutAssigns(ByteSink sink, MuaModel model)
    {
        foreach (int texture in model.Materials)
            sink.Record(MuaSection.Stride[MuaSection.Assign], BaseLayer, (uint)texture);
    }

    private static void PutNames(ByteSink sink, IReadOnlyList<string> names, Strings strings, int stride)
    {
        foreach (string name in names)
            sink.Record(stride, (uint)strings.Of(name));
    }

    private static void PutOrder(ByteSink sink, MuaModel model)
    {
        for (int i = 0; i < model.Meshes.Count; ++i)
        {
            for (int part = 0; part < model.Meshes[i].Parts; ++part)
                sink.Record(MuaSection.Stride[MuaSection.Order], (uint)i, (uint)part);
        }
    }

    private static void PutVertices(ByteSink sink, MuaModel model)
    {
        foreach (MuaVertex vertex in model.Vertices)
        {
            sink.Floats(vertex.Position);
            sink.Floats(vertex.Normal);
            sink.Floats(vertex.Tangent);
            sink.Float(vertex.U);
            sink.Float(vertex.V);
            sink.Float(0.0f);
            sink.Float(0.0f);
            sink.Byte(vertex.Colour.B);
            sink.Byte(vertex.Colour.G);
            sink.Byte(vertex.Colour.R);
            sink.Byte(vertex.Colour.A);
            sink.Floats(vertex.BoneIndex);
            sink.Floats(vertex.Weight);
        }
    }

    private static void PutStringInfo(ByteSink sink, Strings strings)
    {
        int offset = 0;

        foreach (string text in strings.All)
        {
            sink.Record(MuaSection.Stride[MuaSection.StringInfo], (uint)offset, (uint)text.Length);
            offset += text.Length + 1;
        }
    }

    private static void PutStrings(ByteSink sink, Strings strings)
    {
        foreach (string text in strings.All)
        {
            sink.Text(text);
            sink.Byte(0);
        }
    }
}
