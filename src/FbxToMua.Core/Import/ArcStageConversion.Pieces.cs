using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.Mua;

namespace FbxToMua.Core.Import;

internal sealed partial class ArcStageConversion
{
    private int ShineOf(int material)
    {
        List<int> shines = _model.Reflections;

        if (material < 0 || material >= shines.Count || material >= _model.Materials.Count || _state.Mirrored.Contains(material))
            return -1;

        List<int> assigned = _model.Materials[material];
        int shine = shines[material];

        if (assigned.Count == 0 || shine < 0 || shine >= _atlas.Count || _built.Materials[material].TextureIndex != assigned[0])
            return -1;

        return shine;
    }

    private bool ShiningPieces(MuaReadMesh mesh, MeshBody body, Look look)
    {
        if ((SkeletonFlags(mesh) & Shining) == 0 || body.Submeshes.Count == 0)
            return false;

        List<FbxExSubmesh> glowing = FbxExSubmesh.Clone(body.Submeshes);

        foreach (FbxExSubmesh submesh in glowing)
        {
            int shine = ShineOf(submesh.Material);

            if (shine < 0)
                return false;

            submesh.Material = _atlas.Material(submesh.Material, shine);
        }

        int lamp = LampOfMesh(mesh);
        List<float> mapped = [.. body.Vertices];
        List<int> rigged = [.. body.Rigged];
        List<FbxExSubmesh> finer = FbxExSubmesh.Clone(body.Submeshes);
        ArcGeometry.Subdivided(mapped, rigged, finer);
        ArcGeometry.ScreenMapped(mapped, ArcLooks.LensOf(_input.Game));
        ArcGeometry.Mark(mapped, lamp);
        Add(look, mapped, rigged, finer, null, -1);

        List<float> lit = [.. body.Vertices];
        ArcGeometry.Mark(lit, lamp);
        Add(look with { Clear = true, Adds = true }, lit, body.Rigged, glowing, null, -1);

        return true;
    }

    private bool SpritePieces(int index, MuaReadMesh mesh, MeshBody body, Look look, Sheeted plated)
    {
        if (!_state.Sprites.TryGetValue(index, out EvbSprite? sprite))
            return false;

        bool lamped = _state.LampedRects.Contains(index);

        if (!lamped && FlipPieces(mesh, sprite, body, look, plated))
            return true;

        int before = _state.Pieces.Count;
        string stem = Stem(ScriptOf(mesh));
        int lamp = LampOfMesh(mesh);

        for (int r = 0; r < sprite.Rect.Count; ++r)
        {
            EvbRect rect = sprite.Rect[r];

            if (rect.IsSpeck)
                continue;

            Sheeted sheeted = _atlas.SheetFor(sprite, rect, plated);

            if (sheeted.Size is null)
                continue;

            List<float> framed = ArcGeometry.Framed(body.Vertices, sheeted.Rect, sheeted.Size.Value, ArcGeometry.Flip);
            ArcGeometry.Mark(framed, lamped ? LampOf(stem, r) : lamp);
            ArcGeometry.MarkFlow(framed, FlowMarkOf(body.Submeshes));
            Add(look, framed, body.Rigged, Retextured(body.Submeshes, sheeted.Sheet), lamped ? null : sprite.Frame, r);
        }

        return _state.Pieces.Count != before;
    }

    private bool FlipPieces(MuaReadMesh mesh, EvbSprite sprite, MeshBody body, Look look, Sheeted plated)
    {
        SortedDictionary<int, List<int>> sheets = [];
        Sheeted[] placed = Enumerable.Repeat(plated, sprite.Rect.Count).ToArray();

        for (int r = 0; r < sprite.Rect.Count; ++r)
        {
            if (sprite.Rect[r].IsSpeck)
                continue;

            placed[r] = _atlas.SheetFor(sprite, sprite.Rect[r], plated);

            if (placed[r].Size is null)
                continue;

            if (!sheets.TryGetValue(placed[r].Sheet, out List<int>? rects))
                sheets[placed[r].Sheet] = rects = [];

            rects.Add(r);
        }

        if (sheets.Count == 0)
            return false;

        float flow = FlowMarkOf(body.Submeshes);
        List<EvbFlip> wanted = sheets.Values.Select(rects => FlipOf(sprite, rects, placed, flow)).ToList();
        int fresh = wanted.Count(flip => SlotOf(flip) < 0);

        if (_out.Flips.Count + fresh > ImMarks.FlipSlots)
            return false;

        int lamp = LampOfMesh(mesh);
        int next = 0;

        foreach (int sheet in sheets.Keys)
        {
            EvbFlip flip = wanted[next++];
            int slot = SlotOf(flip);

            if (slot < 0)
            {
                slot = _out.Flips.Count;
                _out.Flips.Add(flip);
            }

            List<float> marked = ArcGeometry.Squeezed(body.Vertices);
            ArcGeometry.Shift(marked, ArcGeometry.U, -ImMarks.FlipMark * (slot + 1));
            ArcGeometry.Mark(marked, lamp);
            Add(look, marked, body.Rigged, Retextured(body.Submeshes, sheet), null, -1);
        }

        return true;
    }

    private int SlotOf(EvbFlip flip)
    {
        return _out.Flips.FindIndex(one => one.Rects.SequenceEqual(flip.Rects) && one.Frame.SequenceEqual(flip.Frame));
    }

    private static EvbFlip FlipOf(EvbSprite sprite, List<int> rects, Sheeted[] placed, float flow)
    {
        EvbFlip flip = new();
        Dictionary<int, int> local = [];

        foreach (int r in rects)
        {
            EvbRect rect = placed[r].Rect;
            ArtSize size = placed[r].Size!.Value;
            local[r] = flip.Rects.Count / 4;
            flip.Rects.Add(flow + (float)rect.X / size.Width);
            flip.Rects.Add((float)rect.W / size.Width);
            flip.Rects.Add((float)rect.Y / size.Height);
            flip.Rects.Add((float)rect.H / size.Height);
        }

        foreach (int shown in sprite.Frame)
            flip.Frame.Add(local.TryGetValue(shown, out int at) ? at : -1);

        return flip;
    }

    private List<FbxExSubmesh> Retextured(List<FbxExSubmesh> submeshes, int sheet)
    {
        List<FbxExSubmesh> retextured = FbxExSubmesh.Clone(submeshes);

        foreach (FbxExSubmesh submesh in retextured)
            submesh.Material = _atlas.Material(submesh.Material, sheet);

        return retextured;
    }

    private void Add(Look look, List<float> vertices, List<int> rigged, List<FbxExSubmesh> submeshes, List<int>? frames, int rect)
    {
        List<(int Bone, FbxExNode Node)> groups = look.Animated
            ? ArcGeometry.Split(vertices, rigged, submeshes, 0)
            : [(-1, new FbxExNode { Type = FbxExNode.MeshType, Vertices = [.. vertices], Submeshes = FbxExSubmesh.Clone(submeshes) })];

        foreach ((int bone, FbxExNode node) in groups)
        {
            ArcPiece piece = Made(look, bone, node);
            piece.Run = look.Animated ? look.Run : null;
            piece.Frames = frames;
            piece.Rect = rect;
            _state.Pieces.Add(piece);
        }
    }

    private static ArcPiece Made(Look look, int bone, FbxExNode node, List<float>? fixedAnime = null)
    {
        FbxExNode copy = node.Clone();
        copy.BlendMode = look.Adds ? 1 : 0;

        return new ArcPiece
        {
            Clear = look.Clear,
            Solid = look.Solid,
            Rank = look.Rank,
            Bone = bone,
            Root = look.Root,
            Node = copy,
            Fixed = fixedAnime ?? [],
        };
    }

    private bool BandedPieces(MeshBody body, Look look, Sheeted plated)
    {
        if (look.Animated)
            return false;

        List<Plate> siblings = _state.Siblings.GetValueOrDefault(plated.Sheet) ?? [];
        (int window, int taken) = ArcGeometry.Bands(body.Vertices, body.Submeshes, plated.Size, siblings);

        if (window <= 1)
            return false;

        List<int> wanted = Enumerable.Range(0, window).Where(i => i != taken).ToList();
        int phase = _state.Pieces.Count % wanted.Count;

        for (int step = 0; step < wanted.Count; ++step)
        {
            FbxExNode node = new() { Type = FbxExNode.MeshType, Vertices = ArcGeometry.Banded(body.Vertices, wanted[step], window), Submeshes = FbxExSubmesh.Clone(body.Submeshes) };
            _state.Pieces.Add(Made(look, -1, node, ArcGeometry.Shown(step, wanted.Count, phase)));
        }

        return true;
    }

    private float FlowMarkOf(List<FbxExSubmesh> submeshes)
    {
        foreach (FbxExSubmesh submesh in submeshes)
        {
            if (!_state.Flowing.TryGetValue(submesh.Material, out (int Slot, int Kind) slot))
                continue;

            int which = slot.Slot;
            int group = slot.Kind + FlowKinds * (which / FlowBank);

            return FlowMark * (which % FlowBank + group * FlowBank + 1);
        }

        return 0.0f;
    }

    private void WholePieces(MuaReadMesh mesh, MeshBody body, Look look)
    {
        ArcGeometry.Mark(body.Vertices, LampOfMesh(mesh));
        ArcGeometry.MarkFlow(body.Vertices, FlowMarkOf(body.Submeshes));
        Add(look, body.Vertices, body.Rigged, body.Submeshes, null, -1);
    }
}
