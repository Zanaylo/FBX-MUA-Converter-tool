using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.Fpac;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Import;

internal sealed partial class ArcStageConversion
{
    private const int FlowDown = 0;
    private const int FlowAcross = 1;
    private const int FlowBoth = 2;
    private const int FlowSlots = 16;
    private const int FlowBank = 8;
    private const int FlowKinds = 3;
    private const float FlowMark = 128.0f;
    private const double FullRamp = 1000.0;
    private const int BlendOpaque = 0;
    private const int BlendAdd = 2;
    private const int BlendSubtract = 4;
    private const int BlendUnset = 0x7fffffff;
    private const int LampRects = 4;
    private const uint CullNone = 0x400;
    private const uint PivotMoves = 0x200;
    private const uint Shining = 0x100000;
    private const string SparkSuffix = "_particles.dds";
    private const string KickSuffix = "_kicked.dds";
    private const string KickEffect = "efbg_sakura";
    private const int KickKind = 6;
    private const int KickBursts = 96;
    private const string ObjectFile = "object.txt";

    private static readonly string[] UnculledStages = ["bg_town", "bg_garden", "bg_monolis"];

    private readonly ArcStageInput _input;
    private readonly MuaReader _model;
    private readonly ArcStageResult _out;
    private readonly bool _unculled;
    private readonly FbxExModel _built = new();
    private readonly ArcAtlas _atlas;
    private readonly ArcConversionState _state = new();

    private sealed class MeshBody
    {
        public List<float> Vertices { get; set; } = [];
        public List<int> Rigged { get; set; } = [];
        public List<FbxExSubmesh> Submeshes { get; set; } = [];
        public bool Faded { get; set; }
        public bool Clear { get; set; }
    }

    private sealed record Look(int Root, bool Clear, bool Adds, bool Animated, EvbRun? Run, bool Solid, Rank Rank);

    public ArcStageConversion(ArcStageInput input, MuaReader model, ArcStageResult result)
    {
        _input = input;
        _model = model;
        _out = result;
        _unculled = UnculledStages.Contains(ArcMotion.Lowered(input.Stage));
        _atlas = new ArcAtlas(_built, result.Images);
    }

    public bool Run()
    {
        Paint();
        Flows();
        Strips();
        ReadScene();
        Particles();
        Kicks();

        for (int i = 0; i < _model.Meshes.Count; ++i)
            Classify(i);

        ArcMotion.Internal(_model, _state.Moving);
        BindTakes();

        for (int i = 0; i < _model.Meshes.Count; ++i)
            Build(i);

        if (_state.Pieces.Count == 0)
            return false;

        Assemble();

        return _out.Model.Length > 0;
    }

    private static FbxExMaterial Painted(List<string> textures, int index)
    {
        float[] value = new float[FbxExMaterial.Values];
        value[0] = value[1] = value[2] = value[3] = 1.0f;
        value[7] = value[11] = value[15] = 1.0f;
        value[16] = 20.0f;

        return new FbxExMaterial { FileName = textures.Count == 0 ? string.Empty : textures[index], TextureIndex = index, Value = value };
    }

    private void Paint()
    {
        foreach (string name in _model.Textures)
            _atlas.Add(name);

        for (int m = 0; m < _model.Materials.Count; ++m)
        {
            List<int> assigned = _model.Materials[m];
            int index = assigned.Count == 0 ? 0 : assigned[0];

            if (index < 0 || index >= _atlas.Count)
                index = 0;

            int shine = m < _model.Reflections.Count ? _model.Reflections[m] : -1;
            bool reflects = shine >= 0 && shine < _atlas.Count;

            if (reflects && _atlas.SheetAt(index).Hidden)
                _state.Mirrored.Add(m);

            if (reflects && (_atlas.SheetAt(index).Dark || _atlas.SheetAt(index).Hidden))
                index = shine;

            _built.Materials.Add(Painted(_built.Textures, index));
        }

        if (_built.Materials.Count == 0)
            _built.Materials.Add(Painted(_built.Textures, 0));
    }

    private static int SingleKind(MuaFlow flow) => MathF.Abs(flow.Across) > MathF.Abs(flow.Down) ? FlowAcross : FlowDown;

    private static List<float> FlowRates(MuaFlow flow, int kind)
    {
        List<float> rates = [];

        if (kind != FlowDown)
            rates.Add(-flow.Across);

        if (kind != FlowAcross)
            rates.Add(ArcGeometry.Flip ? flow.Down : -flow.Down);

        return rates;
    }

    private static int SharedSlot(List<float> rates, List<float> wanted)
    {
        for (int at = 0; at + wanted.Count <= rates.Count; ++at)
        {
            if (rates.GetRange(at, wanted.Count).SequenceEqual(wanted))
                return at;
        }

        return -1;
    }

    private void Flows()
    {
        for (int m = 0; m < _model.Flows.Count; ++m)
        {
            MuaFlow flow = _model.Flows[m];

            if (!flow.Known)
                continue;

            int kind = flow.Across != 0.0f && flow.Down != 0.0f ? FlowBoth : SingleKind(flow);
            List<float> wanted = FlowRates(flow, kind);
            int slot = SharedSlot(_out.Flow, wanted);

            if (slot < 0 && kind == FlowBoth && _out.Flow.Count + wanted.Count > FlowSlots)
            {
                kind = SingleKind(flow);
                wanted = FlowRates(flow, kind);
                slot = SharedSlot(_out.Flow, wanted);
            }

            if (slot < 0 && _out.Flow.Count + wanted.Count > FlowSlots)
                continue;

            if (slot < 0)
            {
                slot = _out.Flow.Count;
                _out.Flow.AddRange(wanted);
            }

            _state.Flowing[m] = (slot, kind);
        }
    }

    private void Strips()
    {
        foreach (MuaReadMesh mesh in _model.Meshes)
        {
            if (mesh.Vertices != 4 || mesh.Parts < 1)
                continue;

            Float3[] points = new Float3[4];
            float lowV = 0.0f;
            float highV = 0.0f;

            for (int v = 0; v < 4; ++v)
            {
                MuaReadVertex raw = _model.VertexAt(mesh.FirstVertex + v);
                points[v] = ArcGeometry.Place(raw.Position, ArcGeometry.Scale, ArcGeometry.Mirror);
                lowV = v == 0 ? raw.V : Std.Min(lowV, raw.V);
                highV = v == 0 ? raw.V : Std.Max(highV, raw.V);
            }

            int index = mesh.FirstPart;

            if (index < 0 || index >= _model.Parts.Count)
                continue;

            int material = _model.Parts[index].Material;

            if (material < 0 || material >= _model.Materials.Count || _model.Materials[material].Count == 0)
                continue;

            (_, float tall) = ArcGeometry.Sides(points);
            int sheet = _model.Materials[material][0];

            if (!_state.Siblings.TryGetValue(sheet, out List<Plate>? plates))
                _state.Siblings[sheet] = plates = [];

            plates.Add(new Plate(highV - lowV, tall, 1.0f - highV));
        }
    }

    private void ReadScene()
    {
        _state.Scene = Fpac.Walk(_input.Scene);
        _state.Files = ArcMotion.Motions(_state.Scene);

        foreach ((string path, byte[] data) in _state.Scene)
        {
            string lowered = ArcMotion.Lowered(path);

            if (!lowered.EndsWith(".evb", StringComparison.Ordinal) || !lowered.StartsWith("scr/", StringComparison.Ordinal))
                continue;

            string leaf = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
            string stem = leaf[..^4];
            _state.Every[stem] = data;

            if (stem is "base" or "setting")
            {
                _out.Tilt = EvbPlayer.Tilt(data) ?? _out.Tilt;
                List<EvbZone> zones = EvbPlayer.Zones(data);

                if (zones.Count > 0)
                    _state.Zones = zones;

                continue;
            }

            _state.Scripts[stem] = data;
        }
    }

    private (List<ParticleEffect> Effects, ParticleSurface Surface)? ParticleData()
    {
        List<ParticleEffect>? effects = Import.Particles.Read(_input.Particles);
        ParticleSurface? surface = effects is null ? null : Import.Particles.Atlas(_input.ParticleArt);

        return effects is null || surface is null ? null : (effects, surface);
    }

    private void Kicks()
    {
        if (!_state.Zones.Any(zone => zone.Kind == KickKind))
            return;

        if (ParticleData() is not var (effects, surface))
            return;

        _state.Kicked = ParticleBake.Kicked(effects, KickEffect, surface, KickBursts);
    }

    private void Particles()
    {
        if (ParticleData() is not var (effects, surface))
            return;

        List<SpawnedEffect> spawned = [];

        foreach ((string stem, byte[] script) in _state.Every)
        {
            foreach (EvbSpawn spawn in EvbPlayer.Spawns(script) ?? [])
            {
                SpawnedEffect? entry = spawned.FirstOrDefault(one => one.Effect == spawn.Effect);

                if (entry is null)
                {
                    entry = new SpawnedEffect(spawn.Effect, []);
                    spawned.Add(entry);
                }

                SpawnOrigins(stem, spawn.Bone, entry.Origins);
            }
        }

        if (spawned.Count == 0)
            return;

        _state.Cards = ParticleBake.Emitted(effects, spawned, surface);
        BakedLayer? layer = ParticleBake.Bake(effects, spawned, surface, _input.Stage);

        if (layer is null)
            return;

        _out.Layer[layer.PatName] = layer.Pat;
        _out.Layer[ObjectFile] = layer.Objects;
    }

    private void SpawnOrigins(string stem, int bone, List<double[]> origins)
    {
        string wanted = ArcMotion.Lowered(stem) + ".evb";
        int before = origins.Count;

        foreach (MuaReadSkeleton skeleton in _model.Skeletons)
        {
            int which = skeleton.Script;

            if (which < 0 || which >= _model.Scripts.Count || ArcMotion.Lowered(_model.Scripts[which]) != wanted)
                continue;

            Matrix world = _model.World(skeleton.FirstBone + (bone >= 0 && bone < skeleton.Bones ? bone : 0));
            origins.Add([world[12], world[13], world[14]]);
        }

        if (origins.Count == before)
            origins.Add([0.0, 0.0, 0.0]);
    }
}
