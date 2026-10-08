using FbxToMua.Core.Formats.Dds;
using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.Mmot;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Formats.Objects;
using FbxToMua.Core.Formats.Pat;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public sealed class StageExporter
{
    private const string SceneScript = "base.evb";
    private const string SceneBone = "Bone_setting";
    private const string BonePrefix = "Bone_";
    private const string TakeSuffix = "_000.mmot";
    private const string TakeLabel = ".DIG";
    private const string ScriptSuffix = ".evb";
    private const string FallbackTexture = "white.dds";
    private const string FarSuffix = "_far";
    private const string FrameSuffix = "_frame";
    private const int Additive = 1;
    private const int None = -1;
    private const float Tiny = 1e-12f;
    private const int SpanSamples = 64;
    private const int TierPulled = 0;
    private const int TierStage = 1;
    private const int TierLayer = 2;
    private const int FirstSpriteBone = 2;
    private const uint LayerFlags = MuaFlags.NoDepthTest | MuaFlags.NoDepthWrite | MuaFlags.BothFaces;

    private static readonly int[] QuadTriangles = [0, 1, 2, 0, 2, 3];
    private static readonly int[] SceneSwitches = [0, 1, 0, 0];
    private static readonly int[] SceneVector = [0, 900, -1000];
    private static readonly int[] ScenePair = [0, 8000];
    private static readonly int[] SceneHeight = [750];

    private sealed class Chunk
    {
        public List<int> Sources { get; } = [];
        public int[] Map { get; init; } = [];
        public List<(int Material, List<int> Indices)> Parts { get; } = [];
    }

    private sealed record Limb(string Name, int Parent, Pose Rest, List<Pose> Poses);

    private sealed class NodePlan
    {
        public int Node { get; init; }
        public List<Chunk> Chunks { get; init; } = [];
        public Rig? Rig { get; init; }
        public Matrix World;
        public bool Moving => Rig is not null;
    }

    private readonly record struct DrawKey(int Tier, int Prio, int Order);

    private readonly ExportSource _source;
    private readonly FbxExModel _fbx;
    private readonly FbxExHierarchy _scene;
    private readonly ExportResult _result;
    private readonly MuaModel _model = new();
    private readonly List<DrawKey> _keys = [];
    private readonly Dictionary<string, byte[]> _supplied = new(StringComparer.OrdinalIgnoreCase);
    private readonly Matrix _place;

    private StageExporter(ExportSource source, FbxExModel fbx, ExportResult result)
    {
        _source = source;
        _fbx = fbx;
        _scene = new FbxExHierarchy(fbx);
        _result = result;
        _place = source.Framing.Placement();
    }

    public static ExportResult Convert(ExportSource source)
    {
        FbxExModel fbx = FbxExReader.Read(source.Model) ?? throw new StageConversionException("bg.fbx.bin could not be read");
        ExportResult result = new() { Stage = source.Stage };

        new StageExporter(source, fbx, result).Run();

        return result;
    }

    private void Run()
    {
        AddScene();
        AddNodes();

        if (_model.Meshes.Count == 0)
            throw new StageConversionException("the stage has no meshes to export");

        AddLayer();
        Order();
        AddImages();

        _result.Model = MuaWriter.Build(_model, true);
        _result.Bare = MuaWriter.Build(_model, false);
        _result.Meshes = _model.Meshes.Count;
        _result.Turned = _source.Framing.Turn != 0.0f;
    }

    private void AddScene()
    {
        _model.Scripts.Add(SceneScript);
        _model.Bones.Add(MuaBone.Root(SceneBone));
        _model.Skeletons.Add(new MuaSkeleton(0, 1, 0, MuaBlend.Unset, MuaFlags.Scene));

        List<EvbRecord> records =
        [
            new(EvbCode.SceneOpen),
            new(EvbCode.SceneSwitches, SceneSwitches),
            new(EvbCode.SceneVector, SceneVector),
            new(EvbCode.ScenePair, ScenePair),
            new(EvbCode.SceneHeight, SceneHeight),
        ];

        int tilt = Std.RoundHalfAway(_source.Framing.Tilt);

        if (tilt != 0)
            records.Add(new EvbRecord(EvbCode.SceneTilt, tilt, 0, 0));

        records.Add(new EvbRecord(EvbCode.SceneClose));
        _result.Scripts.Add(new ExportFile(SceneScript, EvbWriter.Build(new EvbScript([], [], records))));
    }

    private int TextureNamed(string name)
    {
        int known = _model.Textures.FindIndex(texture => string.Equals(texture, name, StringComparison.OrdinalIgnoreCase));

        if (known >= 0)
            return known;

        _model.Textures.Add(name);
        _model.Materials.Add(_model.Textures.Count - 1);

        return _model.Textures.Count - 1;
    }

    private int TextureOf(int fbxTexture)
    {
        bool known = fbxTexture >= 0 && fbxTexture < _fbx.Textures.Count;
        string leaf = known ? LeafOf(_fbx.Textures[fbxTexture]) : string.Empty;

        return TextureNamed(leaf.Length == 0 ? FallbackTexture : leaf);
    }

    private int MaterialOf(int fbxMaterial)
    {
        bool known = fbxMaterial >= 0 && fbxMaterial < _fbx.Materials.Count;

        return TextureOf(known ? _fbx.Materials[fbxMaterial].TextureIndex : None);
    }

    private static string LeafOf(string path)
    {
        int slash = path.LastIndexOfAny(['\\', '/']);

        return slash < 0 ? path : path[(slash + 1)..];
    }

    private static Float3 Transform(Float3 point, Matrix world, Matrix place)
    {
        double[] placed = new double[3];

        for (int c = 0; c < 3; ++c)
        {
            placed[c] = (double)point[0] * world[c] + (double)point[1] * world[4 + c] + (double)point[2] * world[8 + c] + world[12 + c];
        }

        Float3 moved = new();

        for (int c = 0; c < 3; ++c)
            moved[c] = (float)(placed[0] * place[c] + placed[1] * place[4 + c] + placed[2] * place[8 + c] + place[12 + c]);

        return moved;
    }

    private static bool TryNormalise(ref Float3 vector)
    {
        float length = MathF.Sqrt(vector[0] * vector[0] + vector[1] * vector[1] + vector[2] * vector[2]);

        if (length < Tiny)
            return false;

        for (int k = 0; k < 3; ++k)
            vector[k] /= length;

        return true;
    }

    private static byte Byte(float value)
    {
        float scaled = value * 255.0f + 0.5f;

        return (byte)(scaled < 0.0f ? 0.0f : scaled > 255.0f ? 255.0f : scaled);
    }

    private static Float3 Slice3(List<float> values, int at) => new(values[at], values[at + 1], values[at + 2]);

    private static void Tangents(List<MuaVertex> vertices, List<int> triangles)
    {
        float[] sum = new float[vertices.Count * 3];

        for (int i = 0; i + 2 < triangles.Count; i += 3)
        {
            MuaVertex a = vertices[triangles[i]];
            MuaVertex b = vertices[triangles[i + 1]];
            MuaVertex c = vertices[triangles[i + 2]];
            Float3 first = new();
            Float3 second = new();

            for (int k = 0; k < 3; ++k)
            {
                first[k] = b.Position[k] - a.Position[k];
                second[k] = c.Position[k] - a.Position[k];
            }

            float du1 = b.U - a.U;
            float dv1 = b.V - a.V;
            float du2 = c.U - a.U;
            float dv2 = c.V - a.V;
            float area = du1 * dv2 - du2 * dv1;

            if (MathF.Abs(area) < Tiny)
                continue;

            for (int corner = 0; corner < 3; ++corner)
            {
                for (int k = 0; k < 3; ++k)
                    sum[triangles[i + corner] * 3 + k] += (first[k] * dv2 - second[k] * dv1) / area;
            }
        }

        for (int v = 0; v < vertices.Count; ++v)
        {
            MuaVertex vertex = vertices[v];
            Float3 tangent = new(sum[v * 3], sum[v * 3 + 1], sum[v * 3 + 2]);
            float along = tangent[0] * vertex.Normal[0] + tangent[1] * vertex.Normal[1] + tangent[2] * vertex.Normal[2];

            for (int k = 0; k < 3; ++k)
                tangent[k] -= vertex.Normal[k] * along;

            if (!TryNormalise(ref tangent))
            {
                Float3 side = new(vertex.Normal[1], -vertex.Normal[0], 0.0f);
                Float3 up = new(0.0f, vertex.Normal[2], -vertex.Normal[1]);
                tangent = MathF.Abs(vertex.Normal[2]) < 0.9f ? side : up;

                if (!TryNormalise(ref tangent))
                    tangent[0] = 1.0f;
            }

            vertex.Tangent = tangent;
            vertices[v] = vertex;
        }
    }

    private MuaVertex Converted(List<float> source, int at, Matrix world, Matrix normals, int bone)
    {
        MuaVertex vertex = MuaVertex.Unrigged();
        vertex.Position = Transform(Slice3(source, at + FbxExNode.PositionAt), world, _place);
        Float3 normal = MatrixMath.Rotate(Slice3(source, at + FbxExNode.NormalAt), normals);

        if (!TryNormalise(ref normal))
            normal[1] = 1.0f;

        vertex.Normal = normal;
        vertex.Colour = new MuaColour(Byte(source[at + FbxExNode.ColourAt]), Byte(source[at + FbxExNode.ColourAt + 1]),
            Byte(source[at + FbxExNode.ColourAt + 2]), Byte(source[at + FbxExNode.ColourAt + 3]));
        vertex.U = source[at + FbxExNode.UvAt];
        vertex.V = 1.0f - source[at + FbxExNode.UvAt + 1];

        return bone == None ? vertex : vertex.RiggedTo(bone);
    }

    private List<Chunk> Chunks(FbxExNode node)
    {
        int count = node.VertexCount;
        List<Chunk> chunks = [new Chunk { Map = Enumerable.Repeat(None, count).ToArray() }];

        foreach (FbxExSubmesh submesh in node.Submeshes)
        {
            int material = MaterialOf(submesh.Material);
            bool opened = false;

            for (int i = 0; i + 2 < submesh.Indices.Count; i += 3)
            {
                int[] corner = [submesh.Indices[i], submesh.Indices[i + 2], submesh.Indices[i + 1]];

                if (corner.Any(v => v < 0 || v >= count))
                    continue;

                int needed = corner.Count(v => chunks[^1].Map[v] == None);

                if (chunks[^1].Sources.Count + needed > MuaVertex.MostPerMesh)
                {
                    chunks.Add(new Chunk { Map = Enumerable.Repeat(None, count).ToArray() });
                    opened = false;
                }

                Chunk chunk = chunks[^1];

                if (!opened)
                {
                    chunk.Parts.Add((material, []));
                    opened = true;
                }

                foreach (int v in corner)
                {
                    if (chunk.Map[v] == None)
                    {
                        chunk.Map[v] = chunk.Sources.Count;
                        chunk.Sources.Add(v);
                    }

                    chunk.Parts[^1].Indices.Add(chunk.Map[v]);
                }
            }
        }

        return chunks.Where(chunk => chunk.Sources.Count > 0).ToList();
    }

    private static string JointName(string name, Rig rig, int k)
    {
        return k + 1 == rig.Joints.Count ? name : $"{name}_{rig.Joints[k].Node}";
    }

    private int AddStill(string name, MuaBlend blend, uint flags)
    {
        int first = _model.Bones.Count;
        MuaBone bone = MuaBone.Root(name);
        bone.Kind = MuaBone.MeshKind;
        _model.Bones.Add(bone);
        _model.Skeletons.Add(new MuaSkeleton(first, 1, MuaSkeleton.NoScript, blend, flags));

        return _model.Skeletons.Count - 1;
    }

    private int AddRig(string name, List<Limb> limbs, MuaBlend blend, uint flags)
    {
        int first = _model.Bones.Count;
        List<int> parents = [None];
        _model.Bones.Add(MuaBone.Root(BonePrefix + name));

        foreach (Limb limb in limbs)
        {
            _model.Bones.Add(MuaBone.Joint(limb.Name, limb.Rest));
            parents.Add(limb.Parent);
        }

        MuaBone.Tree(_model.Bones, first, parents);

        int script = _model.Scripts.Count;
        _model.Scripts.Add(name + ScriptSuffix);
        _model.Skeletons.Add(new MuaSkeleton(first, parents.Count, script, blend, flags));

        return _model.Skeletons.Count - 1;
    }

    private string AddMesh(string name, int skeleton, List<MuaVertex> vertices, List<(int Material, List<int> Indices)> parts, DrawKey key)
    {
        Tangents(vertices, parts.SelectMany(part => part.Indices).ToList());

        MuaMesh mesh = new()
        {
            Name = name,
            Skeleton = skeleton,
            Partner = skeleton,
            FirstPart = _model.Parts.Count,
            Parts = parts.Count,
            FirstVertex = _model.Vertices.Count,
            Vertices = vertices.Count,
        };

        foreach ((int material, List<int> indices) in parts)
        {
            List<ushort> strip = TriangleStrip.Build(indices);
            _model.Parts.Add(new MuaPart(material, _model.Indices.Count, strip.Count));
            _model.Indices.AddRange(strip);
        }

        _model.Vertices.AddRange(vertices);
        _model.Meshes.Add(mesh);
        _keys.Add(key);

        return mesh.Name;
    }

    private NodePlan? Plan(int node)
    {
        List<Chunk> chunks = Chunks(_fbx.Nodes[node]);

        if (chunks.Count == 0)
            return null;

        return new NodePlan
        {
            Node = node,
            Chunks = chunks,
            Rig = NodeRig.Of(_scene, node, _place),
            World = _scene.World(node, 0),
        };
    }

    private static Matrix Chained(Rig rig, int frame)
    {
        Matrix chained = Matrix.Identity;

        foreach (RigJoint joint in rig.Joints)
            chained = MatrixMath.Multiply(PoseMath.Compose(joint.Poses[frame]), chained);

        return chained;
    }

    private static List<Matrix> Motions(NodePlan plan)
    {
        if (plan.Rig is null)
            return [Matrix.Identity];

        Matrix unbind = MatrixMath.Invert(Chained(plan.Rig, 0));
        int step = Math.Max(1, plan.Rig.Span / SpanSamples);
        List<Matrix> motions = [];

        for (int frame = 0; frame < plan.Rig.Span; frame += step)
            motions.Add(MatrixMath.Multiply(unbind, Chained(plan.Rig, frame)));

        return motions;
    }

    private DepthSpan SpanOf(NodePlan plan)
    {
        FbxExNode source = _fbx.Nodes[plan.Node];
        int count = source.VertexCount;
        Float3[] placed = new Float3[count];

        for (int v = 0; v < count; ++v)
            placed[v] = Transform(Slice3(source.Vertices, v * FbxExNode.VertexFloats + FbxExNode.PositionAt), plan.World, _place);

        DepthSpan span = DepthSpan.Empty;

        foreach (Matrix motion in Motions(plan))
        {
            foreach (Float3 point in placed)
                span.Widen(MatrixMath.Transform(point, motion));
        }

        return span;
    }

    private void AddNodes()
    {
        List<NodePlan> plans = [];
        List<DepthSpan> spans = [];

        for (int i = 0; i < _fbx.Nodes.Count; ++i)
        {
            FbxExNode node = _fbx.Nodes[i];

            if (node.Type != FbxExNode.MeshType || node.Vertices.Count == 0 || node.Submeshes.Count == 0)
                continue;

            NodePlan? plan = Plan(i);

            if (plan is null)
                continue;

            spans.Add(SpanOf(plan));
            plans.Add(plan);
        }

        List<FarPull> pulls = FarField.Decide(spans);

        for (int i = 0; i < plans.Count; ++i)
            AddNode(plans[i], pulls[i]);
    }

    private static List<Limb> LimbsOf(string name, Rig rig, FarPull pull)
    {
        List<Limb> limbs = [];

        if (pull.Pulled)
        {
            Pose pose = PoseMath.Split(FarField.Pull(pull.Factor));
            limbs.Add(new Limb(name + FarSuffix, 0, pose, [pose]));
        }

        for (int k = 0; k < rig.Joints.Count; ++k)
            limbs.Add(new Limb(JointName(name, rig, k), limbs.Count, rig.Joints[k].Poses[0], rig.Joints[k].Poses));

        return limbs;
    }

    private void AddNode(NodePlan plan, FarPull pull)
    {
        FbxExNode source = _fbx.Nodes[plan.Node];
        string name = $"node_{plan.Node:D3}";
        Matrix normals = MatrixMath.NormalMatrix(MatrixMath.Multiply(plan.World, _place));
        Matrix pulling = FarField.Pull(pull.Factor);
        MuaBlend blend = source.BlendMode == Additive ? MuaBlend.Add : MuaBlend.Unset;
        uint depth = pull.KeepsDepth ? 0 : MuaFlags.NoDepthWrite;
        List<Limb> limbs = plan.Rig is null ? [] : LimbsOf(name, plan.Rig, pull);
        int bone = plan.Moving ? limbs.Count : None;
        int skeleton = plan.Moving ? AddRig(name, limbs, blend, MuaFlags.Animated | depth) : AddStill(name, blend, MuaFlags.Static | depth);
        DrawKey key = new(pull.Pulled ? TierPulled : TierStage, 0, _keys.Count);
        List<string> meshNames = [];

        for (int c = 0; c < plan.Chunks.Count; ++c)
        {
            Chunk chunk = plan.Chunks[c];
            List<MuaVertex> vertices = [];

            foreach (int v in chunk.Sources)
            {
                MuaVertex vertex = Converted(source.Vertices, v * FbxExNode.VertexFloats, plan.World, normals, bone);
                vertex.Position = MatrixMath.Transform(vertex.Position, pulling);
                vertices.Add(vertex);
            }

            meshNames.Add(AddMesh(c == 0 ? name : $"{name}_{c}", skeleton, vertices, chunk.Parts, key));
        }

        if (plan.Rig is not null)
            AddTake(name, limbs, plan.Rig.Span, meshNames);

        if (!pull.Pulled)
            return;

        _result.Pulls.Add(new PulledNode(plan.Node, pull.Factor));
        ++_result.Pulled;
    }

    private void AddLayer()
    {
        if (_source.Objects.Length == 0 || _source.Sheet.Length == 0)
            return;

        PatDocument sheet = PatReader.Read(_source.Sheet);
        string text = System.Text.Encoding.Latin1.GetString(_source.Objects);
        ObjectLayer layer = ObjectLayer.Convert(ObjectList.Read(text), sheet, _source.SheetName);
        _result.Absent.AddRange(layer.Missing);

        if (layer.Groups.Count == 0)
            return;

        _result.Sprites = layer.Sprites;
        _result.Front = layer.Front;

        foreach (LayerImage image in layer.Atlases)
            _supplied[image.Name] = image.Data;

        foreach (LayerGroup group in layer.Groups)
            AddGroup(group, layer);
    }

    private void AddGroup(LayerGroup group, ObjectLayer layer)
    {
        int count = group.Sprites.Count;

        for (int first = 0; first < count; first += ObjectLayer.MostSprites)
            AddPiece(group, layer, first, Math.Min(count, first + ObjectLayer.MostSprites));
    }

    private static MuaBlend LayerBlend(int blend)
    {
        return (PatBlend)blend switch
        {
            PatBlend.Additive => MuaBlend.Add,
            PatBlend.Subtractive => MuaBlend.Subtract,
            _ => MuaBlend.Unset,
        };
    }

    private void AddPiece(LayerGroup group, ObjectLayer layer, int first, int last)
    {
        string name = $"layer_{group.Entry:D3}_{group.Blend}_{first / ObjectLayer.MostSprites}";
        List<Limb> limbs = [];

        if (group.Moves)
        {
            limbs.Add(new Limb(name + FrameSuffix, 0, group.Frame, [group.Frame]));

            for (int k = first; k < last; ++k)
                limbs.Add(new Limb($"{name}_{k - first}", 1, group.Sprites[k].Rest, group.Sprites[k].Poses));
        }

        MuaBlend blend = LayerBlend(group.Blend);
        int skeleton = group.Moves ? AddRig(name, limbs, blend, MuaFlags.Animated | LayerFlags) : AddStill(name, blend, MuaFlags.Static | LayerFlags);
        Matrix frame = PoseMath.Compose(group.Frame);
        List<MuaVertex> vertices = [];
        List<(int Material, List<int> Indices)> parts = [];

        for (int k = first; k < last; ++k)
        {
            LayerSprite sprite = group.Sprites[k];
            int bone = group.Moves ? k - first + FirstSpriteBone : None;
            List<int> indices = PartFor(parts, TextureNamed(layer.Atlases[sprite.Atlas].Name));
            int baseVertex = vertices.Count;

            AddQuad(sprite, group.Moves ? sprite.Rest : sprite.Poses[0], frame, bone, vertices);

            foreach (int corner in QuadTriangles)
                indices.Add(baseVertex + corner);
        }

        string mesh = AddMesh(name, skeleton, vertices, parts, new DrawKey(TierLayer, group.Prio, _keys.Count));

        if (group.Moves)
            AddTake(name, limbs, group.Span, [mesh]);
    }

    private static List<int> PartFor(List<(int Material, List<int> Indices)> parts, int material)
    {
        foreach ((int Material, List<int> Indices) part in parts)
        {
            if (part.Material == material)
                return part.Indices;
        }

        parts.Add((material, []));

        return parts[^1].Indices;
    }

    private static void AddQuad(LayerSprite sprite, Pose pose, Matrix frame, int bone, List<MuaVertex> vertices)
    {
        Matrix world = MatrixMath.Multiply(PoseMath.Compose(pose), frame);
        Matrix normals = MatrixMath.NormalMatrix(world);

        foreach (LayerCorner corner in sprite.Corners)
        {
            MuaVertex vertex = MuaVertex.Unrigged();
            vertex.Position = MatrixMath.Transform(corner.Position, world);
            Float3 normal = MatrixMath.Rotate(new Float3(0.0f, 0.0f, 1.0f), normals);

            if (!TryNormalise(ref normal))
                normal[2] = -1.0f;

            vertex.Normal = normal;
            vertex.U = corner.U;
            vertex.V = corner.V;
            vertex.Colour = sprite.Colour;
            vertices.Add(bone == None ? vertex : vertex.RiggedTo(bone));
        }
    }

    private void AddTake(string name, List<Limb> limbs, int span, List<string> meshes)
    {
        MmotBone still = MmotBone.Still();
        Pose rest = MuaBone.Root(name).Pose;
        still.Hold(rest, 0);
        still.Hold(rest, span);

        List<MmotBone> bones = [still];
        List<Matrix> worlds = [Matrix.Identity];

        foreach (Limb limb in limbs)
        {
            Matrix parentWorld = worlds[limb.Parent];
            MmotBone bone = MmotBone.Still();
            bone.Local = PoseMath.Compose(limb.Rest);
            bone.ParentUnbind = MatrixMath.Invert(parentWorld);
            Matrix world = MatrixMath.Multiply(bone.Local, parentWorld);
            bone.Unbind = MatrixMath.Invert(world);
            worlds.Add(world);

            if (limb.Poses.Count == 1)
            {
                bone.Hold(limb.Poses[0], 0);
                bone.Hold(limb.Poses[0], span);
            }
            else
            {
                foreach (int frame in KeyReduction.Kept(limb.Poses))
                    bone.Hold(limb.Poses[frame], frame);
            }

            bones.Add(bone);
        }

        string motionName = name + TakeSuffix;
        _result.Motions.Add(new ExportFile(motionName, MmotWriter.Build(new MmotTake(name + TakeLabel, BonePrefix + name, meshes, span, bones))));

        EvbScript script = new([], [motionName], [
            new EvbRecord(EvbCode.Begin), new EvbRecord(EvbCode.Wait, 0), new EvbRecord(EvbCode.Pick, 0),
            new EvbRecord(EvbCode.Close), new EvbRecord(EvbCode.Yield),
        ]);

        _result.Scripts.Add(new ExportFile(name + ScriptSuffix, EvbWriter.Build(script)));
        ++_result.Animated;
    }

    private void Order()
    {
        List<int> ranked = Enumerable.Range(0, _model.Meshes.Count)
            .OrderBy(i => _keys[i].Tier).ThenBy(i => _keys[i].Prio).ThenBy(i => _keys[i].Order).ToList();
        float count = _model.Meshes.Count;

        for (int rank = 0; rank < ranked.Count; ++rank)
            _model.Meshes[ranked[rank]].Pivot[2] = count - rank;
    }

    private void AddImages()
    {
        foreach (string name in _model.Textures)
        {
            byte[]? data = _supplied.GetValueOrDefault(name) ?? _source.Image(name);

            if (data is null && string.Equals(name, FallbackTexture, StringComparison.OrdinalIgnoreCase))
                data = DdsHeader.WhiteDxt1();

            if (data is null || data.Length == 0)
            {
                _result.Missing.Add(name);
                continue;
            }

            if (!DdsHeader.IsDds(data))
                _result.Foreign.Add(name);

            byte[] copy = (byte[])data.Clone();
            DdsHeader.StateLinearSize(copy);
            _result.Images.Add(new ExportFile(name, copy));
        }
    }
}
