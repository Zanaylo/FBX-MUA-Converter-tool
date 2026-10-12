using FbxToMua.Core.Formats.Dds;
using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.Mmot;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Formats.Objects;
using FbxToMua.Core.Formats.Pat;
using FbxToMua.Core.Geometry;
using FbxToMua.Core.Imaging;

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
    private const string ShadowSuffix = "_shadow";
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
    private static readonly PoseChannel[] Channels = Enum.GetValues<PoseChannel>();
    private static readonly int[] SceneSwitches = [0, 1, 0, 0];
    private static readonly int[] ScenePair = [0, 8000];

    private sealed class Chunk
    {
        public List<int> Sources { get; } = [];
        public int[] Map { get; init; } = [];
        public List<(int Material, List<int> Indices)> Parts { get; } = [];
    }

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
    private readonly List<MeshDraft> _drafts = [];
    private readonly RigBook _rigs = new();
    private readonly Dictionary<string, byte[]> _supplied = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _found = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _located = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _solid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Matrix _place;
    private readonly int[] _light;
    private double _unwrittenFront = double.MaxValue;

    private StageExporter(ExportSource source, FbxExModel fbx, ExportResult result)
    {
        _source = source;
        _fbx = fbx;
        _scene = new FbxExHierarchy(fbx);
        _result = result;
        _place = source.Reframe.IsNone ? source.Framing.Placement() : MatrixMath.Multiply(source.Framing.Placement(), source.Reframe.StageMatrix());
        _light = SceneLight.Vector(source.Reframe);
    }

    private StageLook Look => _source.Reframe.Look ?? _source.Framing.Look;

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
            new(EvbCode.SceneVector, _light),
            new(EvbCode.ScenePair, ScenePair),
            new(EvbCode.SceneHeight, SceneLight.Strength(_source.Reframe)),
        ];

        int tilt = _source.Reframe.SceneTilt(_source.Framing.Tilt);
        _result.Tilt = tilt;

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

        return TextureNamed(leaf.Length == 0 ? FallbackTexture : Located(leaf));
    }

    private string Located(string leaf)
    {
        if (_located.TryGetValue(leaf, out string? known))
            return known;

        string located = TextureFile.Candidates(leaf).FirstOrDefault(Readable) ?? Path.ChangeExtension(leaf, TextureFile.Extension);
        _located[leaf] = located;

        return located;
    }

    private bool Readable(string name)
    {
        if (_found.ContainsKey(name))
            return true;

        byte[]? data = _source.Image(name);

        if (data is null || data.Length == 0)
            return false;

        _found[name] = data;

        return true;
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

            if (!Float3.TryNormalise(ref tangent))
            {
                Float3 side = new(vertex.Normal[1], -vertex.Normal[0], 0.0f);
                Float3 up = new(0.0f, vertex.Normal[2], -vertex.Normal[1]);
                tangent = MathF.Abs(vertex.Normal[2]) < 0.9f ? side : up;

                if (!Float3.TryNormalise(ref tangent))
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

        if (!Float3.TryNormalise(ref normal))
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
        _unwrittenFront = GameBloomShield.UnwrittenFront(spans, pulls);
        MeasureDepths(plans);

        for (int i = 0; i < plans.Count; ++i)
            AddNode(plans[i], pulls[i], spans[i]);

        AddDrafts();
    }

    private static List<Limb> LimbsOf(string name, Rig rig, FarPull pull)
    {
        List<Limb> limbs = [];

        if (pull.Pulled)
        {
            Pose pose = PoseMath.Split(FarField.Pull(pull.Factor));
            limbs.Add(new Limb(name + FarSuffix, 0, pose, [pose], new LimbOrigin(LimbOrigin.NoNode, pull.Factor)));
        }

        for (int k = 0; k < rig.Joints.Count; ++k)
        {
            RigJoint joint = rig.Joints[k];
            limbs.Add(new Limb(JointName(name, rig, k), limbs.Count, joint.Poses[0], joint.Poses, new LimbOrigin(joint.Node, 0.0f)));
        }

        return limbs;
    }

    private DraftOwner OwnerOf(NodePlan plan, string name, FarPull pull, MuaBlend blend, uint depth, out int bone)
    {
        bone = None;

        if (plan.Rig is null)
            return new DraftOwner(blend, MuaFlags.Static | depth, null);

        uint flags = MuaFlags.Animated | depth;
        RigPlace place = _rigs.Place(new RigKey(plan.Rig.Span, blend, flags), LimbsOf(name, plan.Rig, pull));
        bone = place.Bone;

        return new DraftOwner(blend, flags, place.Rig);
    }

    private static bool IsPlain(DraftOwner owner, FarPull pull)
    {
        return owner.Rig is null && owner.Blend is (MuaBlend.Unset or MuaBlend.Opaque) && owner.Flags == MuaFlags.Static && !pull.Pulled;
    }

    private static ShadowChunk[] ShadowChunksOf(List<Chunk> chunks, List<List<MuaVertex>> converted)
    {
        return chunks.Select((chunk, c) => new ShadowChunk(converted[c].Select(vertex => vertex.Position).ToArray(), chunk.Parts.Select(part => part.Indices.ToArray()).ToArray())).ToArray();
    }

    private MuaBlend BlendOf(FbxExNode source, NodePlan plan, FarPull pull, DepthSpan span)
    {
        if (source.BlendMode == Additive)
            return MuaBlend.Add;

        return StaysClearOfGameBloom(source, plan, pull, span) ? MuaBlend.Opaque : MuaBlend.Unset;
    }

    private bool StaysClearOfGameBloom(FbxExNode source, NodePlan plan, FarPull pull, DepthSpan span)
    {
        if (!Look.ClearOfGameBloom)
            return false;

        if (!GameBloomShield.DrawsFirstSafely(span, pull, _unwrittenFront))
            return false;

        return HasOpaqueVertices(source) && plan.Chunks.SelectMany(chunk => chunk.Parts).All(part => IsSolid(_model.Textures[_model.Materials[part.Material]]));
    }

    private static bool HasOpaqueVertices(FbxExNode source)
    {
        for (int v = 0; v < source.VertexCount; ++v)
        {
            if (Byte(source.Vertices[v * FbxExNode.VertexFloats + FbxExNode.ColourAt + 3]) != byte.MaxValue)
                return false;
        }

        return true;
    }

    private bool IsSolid(string texture)
    {
        if (_solid.TryGetValue(texture, out bool known))
            return known;

        byte[]? data = ImageData(texture);
        BgraImage? image = data is null ? null : DdsCodec.Decode(data);
        bool solid = image is not null && GameBloomShield.IsSolid(image);
        _solid[texture] = solid;

        return solid;
    }

    private void AddNode(NodePlan plan, FarPull pull, DepthSpan span)
    {
        FbxExNode source = _fbx.Nodes[plan.Node];
        string name = $"node_{plan.Node:D3}";
        Matrix normals = MatrixMath.NormalMatrix(MatrixMath.Multiply(plan.World, _place));
        Matrix pulling = FarField.Pull(pull.Factor);
        MuaBlend blend = BlendOf(source, plan, pull, span);
        uint depth = pull.KeepsDepth ? 0 : MuaFlags.NoDepthWrite;
        DraftOwner owner = OwnerOf(plan, name, pull, blend, depth, out int bone);
        int tier = pull.Pulled ? TierPulled : TierStage;
        List<List<MuaVertex>> converted = [];

        foreach (Chunk chunk in plan.Chunks)
        {
            List<MuaVertex> vertices = [];

            foreach (int v in chunk.Sources)
            {
                MuaVertex vertex = Converted(source.Vertices, v * FbxExNode.VertexFloats, plan.World, normals, bone);
                vertex.Position = MatrixMath.Transform(vertex.Position, pulling);

                if (blend == MuaBlend.Opaque)
                    vertex.Colour = vertex.Colour with { A = GameBloomShield.HiddenAlpha };

                vertices.Add(vertex);
            }

            converted.Add(vertices);
        }

        ShadowCandidate? candidate = IsPlain(owner, pull) ? new ShadowCandidate(ShadowChunksOf(plan.Chunks, converted)) : null;

        if (candidate is not null)
            _result.ShadowCandidates.Add(candidate);

        bool floor = candidate is not null && ShadowReceiver.IsFloor(candidate.Chunks);

        for (int c = 0; c < plan.Chunks.Count; ++c)
        {
            string chunkName = c == 0 ? name : $"{name}_{c}";

            if (floor)
                AddShadowed(chunkName, owner, tier, converted[c], plan.Chunks[c].Parts);
            else
                AddDraft(chunkName, owner, tier, converted[c], plan.Chunks[c].Parts);
        }

        if (plan.Moving)
            ++_result.Animated;

        if (blend == MuaBlend.Opaque)
            ++_result.ClearOfGameBloom;

        if (!pull.Pulled)
            return;

        _result.Pulls.Add(new PulledNode(plan.Node, pull.Factor));
        ++_result.Pulled;
    }

    private void AddShadowed(string name, DraftOwner owner, int tier, List<MuaVertex> vertices, List<(int Material, List<int> Indices)> parts)
    {
        List<IReadOnlyList<int>> triangles = parts.Select(part => (IReadOnlyList<int>)part.Indices).ToList();
        ShadowSplit<MuaVertex> split = ShadowReceiver.Split(vertices, triangles, _light, MuaVertex.MostPerMesh, vertex => vertex.Position, MuaVertex.Between);
        List<int> materials = parts.Select(part => part.Material).ToList();

        if (split.Lit.IsEmpty)
        {
            AddDraft(name, owner, tier, vertices, parts);
            return;
        }

        bool cut = !split.Unlit.IsEmpty;
        AddDraft(cut ? name + ShadowSuffix : name, owner with { Flags = owner.Flags & ~MuaFlags.NoShadow }, tier, split.Lit, materials);
        ++_result.ShadowFloors;

        if (!cut)
            return;

        AddDraft(name, owner, tier, split.Unlit, materials);
        ++_result.ShadowCuts;
    }

    private void AddDraft(string name, DraftOwner owner, int tier, ShadowPiece<MuaVertex> piece, List<int> materials)
    {
        List<(int Material, List<int> Indices)> parts = piece.Parts.Select((indices, k) => (materials[k], indices)).Where(part => part.indices.Count > 0).ToList();
        AddDraft(name, owner, tier, piece.Vertices, parts);
    }

    private void AddDraft(string name, DraftOwner owner, int tier, List<MuaVertex> vertices, List<(int Material, List<int> Indices)> parts)
    {
        _drafts.Add(new MeshDraft
        {
            Name = name,
            Owner = owner,
            Tier = tier,
            Order = _drafts.Count,
            Vertices = vertices,
            Parts = parts,
        });
    }

    private void AddDrafts()
    {
        Dictionary<SharedRig, int> skeletons = _rigs.Rigs.ToDictionary(rig => rig, rig => AddRig(rig.Name, rig.Limbs, rig.Key.Blend, rig.Key.Flags));
        Dictionary<SharedRig, List<string>> bound = _rigs.Rigs.ToDictionary(rig => rig, _ => new List<string>());

        foreach (MeshDraft draft in DraftMerger.Merge(_drafts))
        {
            SharedRig? rig = draft.Owner.Rig;
            int skeleton = rig is null ? AddStill(draft.Name, draft.Owner.Blend, draft.Owner.Flags) : skeletons[rig];
            AddMesh(draft.Name, skeleton, draft.Vertices, draft.Parts, new DrawKey(draft.Tier, 0, draft.Order));

            if (rig is not null)
                bound[rig].Add(draft.Name);
        }

        foreach (SharedRig rig in _rigs.Rigs)
            AddTake(rig.Name, rig.Limbs, rig.Key.Span, bound[rig]);
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

        Pose framePose = FramePose(group);

        if (group.Moves)
        {
            limbs.Add(new Limb(name + FrameSuffix, 0, framePose, [framePose]));

            for (int k = first; k < last; ++k)
                limbs.Add(new Limb($"{name}_{k - first}", 1, group.Sprites[k].Rest, group.Sprites[k].Poses));
        }

        MuaBlend blend = LayerBlend(group.Blend);
        int skeleton = group.Moves ? AddRig(name, limbs, blend, MuaFlags.Animated | LayerFlags) : AddStill(name, blend, MuaFlags.Static | LayerFlags);
        Matrix frame = PoseMath.Compose(framePose);
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

        if (!group.Moves)
            return;

        AddTake(name, limbs, group.Span, [mesh]);
        ++_result.Animated;
    }

    private Pose FramePose(LayerGroup group)
    {
        if (_source.Reframe.IsNone)
            return group.Frame;

        return PoseMath.Split(MatrixMath.Multiply(PoseMath.Compose(group.Frame), _source.Reframe.StageMatrix()));
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

            if (!Float3.TryNormalise(ref normal))
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
        if (_source.ForPreview)
            return;

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
                HoldKept(bone, limb.Poses);
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
    }

    private static void HoldKept(MmotBone bone, List<Pose> poses)
    {
        foreach (PoseChannel channel in Channels)
        {
            foreach (int frame in KeyReduction.Kept(poses, channel))
                bone.Hold(poses[frame], frame, channel);
        }
    }

    private void Order()
    {
        List<int> ranked = Enumerable.Range(0, _model.Meshes.Count)
            .OrderBy(i => _keys[i].Tier).ThenBy(i => _keys[i].Prio).ThenBy(i => _keys[i].Order).ToList();
        float count = _model.Meshes.Count;

        for (int rank = 0; rank < ranked.Count; ++rank)
            _model.Meshes[ranked[rank]].Pivot[2] = count - rank;
    }

    private void MeasureDepths(List<NodePlan> plans)
    {
        Dictionary<int, (double Sum, int Count)> depths = [];

        foreach (NodePlan plan in plans)
        {
            FbxExNode source = _fbx.Nodes[plan.Node];
            double sum = 0.0;

            for (int v = 0; v < source.VertexCount; ++v)
                sum += StageDepth.Of(_source.Framing, Transform(Slice3(source.Vertices, v * FbxExNode.VertexFloats + FbxExNode.PositionAt), plan.World, Matrix.Identity)[2]);

            foreach (int material in plan.Chunks.SelectMany(chunk => chunk.Parts).Select(part => part.Material).Distinct())
            {
                (double total, int count) = depths.GetValueOrDefault(material);
                depths[material] = (total + sum, count + source.VertexCount);
            }
        }

        foreach ((int material, (double total, int count)) in depths)
            _result.Depths[_model.Textures[_model.Materials[material]]] = (float)(total / Math.Max(1, count));
    }

    private byte[]? ImageData(string name)
    {
        byte[]? data = _supplied.GetValueOrDefault(name) ?? _found.GetValueOrDefault(name) ?? _source.Image(name);

        if (data is null && string.Equals(name, FallbackTexture, StringComparison.OrdinalIgnoreCase))
            return DdsHeader.WhiteDxt1();

        return data is null || data.Length == 0 ? null : data;
    }

    private float DepthOf(string texture) => _result.Depths.GetValueOrDefault(texture, StageDepth.Near);

    private void AddImages()
    {
        StageLook look = Look;
        bool baked = !_source.ForPreview;
        _result.OwnLook = _source.Framing.Look;
        List<(string Name, byte[]? Data)> read = _model.Textures.Select(name => (name, ImageData(name))).ToList();

        List<(string Name, byte[]? Data)> finished = read.AsParallel().AsOrdered()
            .Select(one => (one.Name, baked && one.Data is not null && !_supplied.ContainsKey(one.Name) ? look.Baked(one.Data, DepthOf(one.Name)) : one.Data))
            .ToList();

        foreach ((string name, byte[]? data) in finished)
        {
            if (data is null)
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
