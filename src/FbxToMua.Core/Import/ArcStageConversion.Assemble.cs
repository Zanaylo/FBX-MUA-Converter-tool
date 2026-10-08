using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;

namespace FbxToMua.Core.Import;

internal sealed partial class ArcStageConversion
{
    private sealed class Placed(FbxExNode node, List<float> anime, bool clear, bool once)
    {
        public FbxExNode Node { get; } = node;
        public List<float> Anime { get; } = anime;
        public bool Clear { get; } = clear;
        public bool Once { get; } = once;
    }

    private sealed class RankComparer : IComparer<ArcPiece>
    {
        public static readonly RankComparer Instance = new();

        public int Compare(ArcPiece? one, ArcPiece? other) => Rank.Compare(one!.Rank, other!.Rank);
    }

    private List<float> AnimeOf(ArcPiece piece)
    {
        if (piece.Fixed.Count > 0)
            return piece.Fixed;

        List<float> motion = ArcMotion.MotionOf(piece.Root, piece.Bone, piece.Run, _state.Moving);

        if (piece.Frames is not null)
            return ArcGeometry.ParkedMotion(piece.Frames, piece.Rect, motion);

        return motion.Count == 0 ? ArcGeometry.Rest() : motion;
    }

    private static List<ArcPiece> Ordered(List<ArcPiece> pieces)
    {
        List<ArcPiece> ordered = pieces.Where(piece => !piece.Clear).ToList();
        ordered.AddRange(pieces.Where(piece => piece.Clear && piece.Solid).OrderBy(piece => piece, RankComparer.Instance));
        ordered.AddRange(pieces.Where(piece => piece.Clear && !piece.Solid).OrderBy(piece => piece, RankComparer.Instance));

        return ordered;
    }

    private void Assemble()
    {
        List<ArcPiece> ordered = Ordered(_state.Pieces);
        FbxExNode root = FbxExNode.Branch();
        root.Child = ordered.Count == 0 ? -1 : 1;
        _built.Nodes.Add(root);
        _built.Animes.Add(ArcGeometry.Rest());

        List<Placed> placed = [];

        foreach (ArcPiece piece in ordered)
        {
            List<float> anime = AnimeOf(piece);

            if (placed.Count > 0 && ArcGeometry.Mergeable(placed[^1].Node, placed[^1].Anime, placed[^1].Clear, piece.Node, anime, piece.Clear))
            {
                ArcGeometry.Merge(placed[^1].Node, piece.Node);
                continue;
            }

            bool once = piece.Run is not null && piece.Run.Settled && piece.Frames is null && anime.Count > ArcGeometry.MatrixFloats;
            placed.Add(new Placed(piece.Node.Clone(), anime, piece.Clear, once));
        }

        foreach (Placed one in placed)
        {
            if (one.Once)
            {
                _out.Once.Add(_built.Nodes.Count);
                _out.Once.Add(one.Anime.Count / ArcGeometry.MatrixFloats);
            }

            _built.Nodes.Add(one.Node);
            _built.Animes.Add(one.Anime);
        }

        CardNodes(_state.Cards, SparkSuffix);
        KickNodes();

        for (int i = 1; i < _built.Nodes.Count; ++i)
            _built.Nodes[i].Sibling = i + 1 < _built.Nodes.Count ? i + 1 : -1;

        _atlas.Emit(_out.Images);
        _out.Model = FbxExWriter.Build(_built);
    }

    private int CardNodes(ParticleCards? cards, string suffix)
    {
        int first = _built.Nodes.Count;

        if (cards is null || cards.Cards.Count == 0)
            return first;

        string name = ArcMotion.Lowered(_input.Stage) + suffix;
        _out.Images[name] = ArcGeometry.RawDds(cards.Side, cards.Rgba);
        int texture = _atlas.Add(name);
        int material = _built.Materials.Count;
        _built.Materials.Add(Painted(_built.Textures, texture));

        foreach (ParticleCard card in cards.Cards)
        {
            List<float> anime = card.Frames.SelectMany(ArcGeometry.PoseMatrix).ToList();
            _built.Nodes.Add(ArcGeometry.CardQuad(cards.Cells[card.Cell], card.Tint, material));
            _built.Animes.Add(anime);
        }

        return first;
    }

    private void KickNodes()
    {
        ParticleCards? kicked = _state.Kicked;

        if (kicked is null || kicked.Cards.Count == 0)
            return;

        int first = CardNodes(kicked, KickSuffix);
        int perBurst = kicked.Cards.Count / KickBursts;
        _out.Kick.AddRange([first, KickBursts, perBurst, kicked.Cards[0].Frames.Count]);

        foreach (EvbZone zone in _state.Zones)
            _out.Kick.AddRange([zone.Limit, zone.Kind]);
    }
}
