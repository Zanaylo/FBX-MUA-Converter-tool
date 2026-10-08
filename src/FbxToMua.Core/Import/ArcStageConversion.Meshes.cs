using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Import;

internal sealed partial class ArcStageConversion
{
    private string ScriptOf(MuaReadMesh mesh)
    {
        if (mesh.Skeleton < 0 || mesh.Skeleton >= _model.Skeletons.Count)
            return string.Empty;

        int index = _model.Skeletons[mesh.Skeleton].Script;

        if (index < 0 || index >= _model.Scripts.Count)
            return string.Empty;

        string named = _model.Scripts[index];
        int dot = named.LastIndexOf('.');

        return dot < 0 ? named : named[..dot];
    }

    private string Stem(string bound) => _state.Scripts.ContainsKey(bound) ? bound : string.Empty;

    private EvbPlayed PlayedFor(string stem, int skeleton)
    {
        if (_state.Played.TryGetValue((stem, skeleton), out EvbPlayed? known))
            return known;

        EvbPlayed fresh = EvbPlayer.Play(_state.Scripts[stem], $"{stem} {skeleton}") ?? new EvbPlayed();
        _state.Played[(stem, skeleton)] = fresh;

        return fresh;
    }

    private static bool Unseen(EvbPlayed run)
    {
        int first = run.Sample.Count > 1 ? 1 : 0;

        for (int i = first; i < run.Sample.Count; ++i)
        {
            if (run.Sample[i].Ramp > 0.0)
                return false;
        }

        return !run.Sample.Any(sample => sample.Lit);
    }

    private static bool Specks(EvbSprite sprite) => sprite.Rect.Count > 0 && sprite.Rect.All(rect => rect.IsSpeck);

    private static double ConstantRamp(EvbPlayed run)
    {
        if (run.Sample.Count == 0)
            return 0.0;

        double level = run.Sample[0].Ramp;

        if (level <= 0.0 || level >= FullRamp || run.Sample.Any(sample => sample.Ramp != level))
            return 0.0;

        return level / FullRamp;
    }

    private static string RectKey(string stem, int rect) => $"{stem}#{rect}";

    private int LampOf(string stem, int rect) => _state.Slots.TryGetValue(RectKey(stem, rect), out int slot) ? slot : -1;

    private bool TakeRectLamps(EvbPlayed run, EvbSprite sprite, string stem)
    {
        if (_state.Slots.ContainsKey(RectKey(stem, 0)))
            return true;

        EvbLamp?[] wanted = sprite.Rect.Select(rect => EvbPlayer.Showing(run, rect)).ToArray();
        int needed = wanted.Count(lamp => lamp is not null);

        if (_out.Lamps.Count + needed > ImMarks.LampSlots)
            return false;

        for (int r = 0; r < sprite.Rect.Count; ++r)
        {
            if (wanted[r] is null)
            {
                _state.Slots[RectKey(stem, r)] = -1;
                continue;
            }

            _state.Slots[RectKey(stem, r)] = _out.Lamps.Count;
            _out.Lamps.Add(wanted[r]!);
        }

        return true;
    }

    private void Classify(int index)
    {
        MuaReadMesh mesh = _model.Meshes[index];
        string bound = ScriptOf(mesh);
        string stem = Stem(bound);

        if (stem.Length == 0)
            return;

        EvbPlayed run = PlayedFor(stem, mesh.Skeleton);

        if (Unseen(run))
        {
            _state.Unseen.Add(index);
            return;
        }

        double dim = ConstantRamp(run);

        if (dim > 0.0)
            _state.Dimmed[index] = (float)dim;

        EvbSprite? sprite = EvbPlayer.Sprites(run);

        if (sprite is not null && Specks(sprite))
        {
            _state.Unseen.Add(index);
            return;
        }

        if (sprite is not null)
            _state.Sprites[index] = sprite;

        EvbRun? motion = EvbPlayer.Motions(run);

        if (motion is not null)
            _state.Runs[index] = motion;

        if (sprite is not null && EvbPlayer.Long(run) && sprite.Rect.Count <= LampRects && TakeRectLamps(run, sprite, stem))
        {
            _state.LampedRects.Add(index);
            return;
        }

        if (bound != stem || _state.Slots.ContainsKey(bound) || _out.Lamps.Count >= ImMarks.LampSlots)
            return;

        EvbLamp? lamp = EvbPlayer.Lamps(run);

        if (lamp is null)
            return;

        _state.Slots[bound] = _out.Lamps.Count;
        _out.Lamps.Add(lamp);
    }

    private void BindTakes()
    {
        for (int index = 0; index < _model.Meshes.Count; ++index)
        {
            if (!_state.Runs.TryGetValue(index, out EvbRun? run))
                continue;

            foreach (string take in run.Frame.Select(step => step.Take).Distinct().Order(StringComparer.Ordinal))
                ArcMotion.Bind(_model, _state.Files, _model.Meshes[index].Bone, take, _state.Moving);
        }
    }

    private uint SkeletonFlags(MuaReadMesh mesh)
    {
        return mesh.Skeleton >= 0 && mesh.Skeleton < _model.Skeletons.Count ? _model.Skeletons[mesh.Skeleton].Flags : 0;
    }

    private int BlendOf(MuaReadMesh mesh)
    {
        int mode = BlendUnset;

        foreach (int owner in new[] { mesh.Skeleton, mesh.Partner })
        {
            if (owner < 0 || owner >= _model.Skeletons.Count)
                continue;

            int blend = _model.Skeletons[owner].Blend;

            if (blend >= BlendOpaque && blend <= BlendSubtract)
                mode = blend;
        }

        return mode;
    }

    private Rank RankOf(MuaReadMesh mesh)
    {
        int lift = (int)mesh.Pivot[1];
        Rank rank = new(-(float)(int)mesh.Pivot[2], lift < 0 ? lift : 0.0f);

        if ((SkeletonFlags(mesh) & PivotMoves) == 0)
            return rank;

        Matrix world = _model.World(mesh.Bone);
        float depth = mesh.Pivot[2] * world[10] + world[14];
        float w = mesh.Pivot[2] * world[11] + world[15];

        return rank with { First = -(w != 0.0f ? depth / w : depth) };
    }

    private int Shelf(List<FbxExSubmesh> submeshes)
    {
        foreach (FbxExSubmesh submesh in submeshes)
        {
            if (submesh.Material >= 0 && submesh.Material < _built.Materials.Count)
                return _built.Materials[submesh.Material].TextureIndex;
        }

        return 0;
    }

    private MeshBody? Body(int index, int mode)
    {
        MuaReadMesh mesh = _model.Meshes[index];
        (List<float> vertices, List<int> rigged) = ArcGeometry.Vertices(_model, mesh);

        if (vertices.Count == 0)
            return null;

        MeshBody body = new() { Vertices = vertices, Rigged = rigged };
        int rows = vertices.Count / ArcGeometry.Floats;

        if (_state.Dimmed.TryGetValue(index, out float dim))
        {
            for (int i = 0; i < rows; ++i)
                vertices[i * ArcGeometry.Floats + ArcGeometry.Alpha] *= dim;
        }

        if (mode == BlendOpaque)
        {
            for (int i = 0; i < rows; ++i)
                vertices[i * ArcGeometry.Floats + ArcGeometry.Alpha] = 1.0f;
        }

        for (int i = 0; i < rows; ++i)
            body.Faded = body.Faded || vertices[i * ArcGeometry.Floats + ArcGeometry.Alpha] < 1.0f;

        bool twoSided = _unculled || (SkeletonFlags(mesh) & CullNone) != 0;
        body.Submeshes = ArcGeometry.Submeshes(_model, mesh, _built.Materials.Count, twoSided);
        body.Clear = body.Faded;

        if (body.Submeshes.Count > 0 && body.Submeshes.All(submesh => _state.Mirrored.Contains(submesh.Material)))
            ArcGeometry.Reflect(vertices);

        foreach (FbxExSubmesh submesh in body.Submeshes)
            body.Clear = body.Clear || _atlas.Cutout(_built.Materials[submesh.Material].TextureIndex);

        return body.Submeshes.Count > 0 ? body : null;
    }

    private int LampOfMesh(MuaReadMesh mesh) => _state.Slots.TryGetValue(ScriptOf(mesh), out int slot) ? slot : -1;

    private void Build(int index)
    {
        if (_state.Unseen.Contains(index))
            return;

        MuaReadMesh mesh = _model.Meshes[index];
        int mode = BlendOf(mesh);
        MeshBody? body = Body(index, mode);

        if (body is null)
            return;

        int plate = Shelf(body.Submeshes);
        _out.Fading = _out.Fading || body.Faded;

        Look look = new(mesh.Bone, body.Clear || mode != BlendOpaque, mode == BlendAdd, _state.Moving.ContainsKey(mesh.Bone),
            _state.Runs.GetValueOrDefault(index), mode == BlendOpaque, RankOf(mesh));
        Sheeted plated = new(plate, ArtSheet.Measure(_atlas.PixelsAt(plate)), default);

        if (ShiningPieces(mesh, body, look) || SpritePieces(index, mesh, body, look, plated) || BandedPieces(body, look, plated))
            return;

        WholePieces(mesh, body, look);
    }
}
