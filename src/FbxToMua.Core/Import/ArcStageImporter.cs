using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.Fpac;
using FbxToMua.Core.Formats.Mmot;
using FbxToMua.Core.Formats.Mua;

namespace FbxToMua.Core.Import;

public static class ArcStageImporter
{
    public static bool HoldsWholeModel(byte[] archive) => ModelIn(archive)?.HasGeometry ?? false;

    public static ArcStageResult? Convert(ArcStageInput input)
    {
        MuaReader? model = ModelIn(input.Geometry);

        if (model is null)
            return null;

        ArcStageResult result = new();

        foreach ((string path, byte[] data) in Fpac.Walk(input.Art))
            result.Images[ArcMotion.Lowered(path[(path.LastIndexOfAny(['/', '\\']) + 1)..])] = data;

        if (model.Textures.Count == 0)
            return null;

        return new ArcStageConversion(input, model, result).Run() ? result : null;
    }

    private static MuaReader? ModelIn(byte[] archive)
    {
        byte[]? found = Fpac.Ending(Fpac.Walk(archive), ".mua");

        return found is null ? null : MuaReader.Read(found);
    }
}

internal sealed class ArcPiece
{
    public bool Clear { get; init; }
    public bool Solid { get; init; } = true;
    public Rank Rank { get; init; }
    public int Bone { get; init; } = -1;
    public int Root { get; init; } = -1;
    public FbxExNode Node { get; init; } = FbxExNode.Leaf();
    public List<float> Fixed { get; init; } = [];
    public EvbRun? Run { get; set; }
    public List<int>? Frames { get; set; }
    public int Rect { get; set; } = -1;
}

internal sealed class ArcConversionState
{
    public SortedDictionary<int, (int Slot, int Kind)> Flowing { get; set; } = [];
    public SortedDictionary<int, List<Plate>> Siblings { get; set; } = [];
    public Dictionary<int, Dictionary<string, Track>> Moving { get; } = [];
    public SortedDictionary<string, MmotMotion> Files { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, byte[]> Scene { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, byte[]> Every { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, byte[]> Scripts { get; } = new(StringComparer.Ordinal);
    public Dictionary<(string, int), EvbPlayed> Played { get; } = [];
    public Dictionary<int, EvbSprite> Sprites { get; } = [];
    public Dictionary<int, EvbRun> Runs { get; } = [];
    public Dictionary<string, int> Slots { get; } = new(StringComparer.Ordinal);
    public HashSet<int> LampedRects { get; } = [];
    public HashSet<int> Unseen { get; } = [];
    public Dictionary<int, float> Dimmed { get; } = [];
    public HashSet<int> Mirrored { get; } = [];
    public List<ArcPiece> Pieces { get; } = [];
    public ParticleCards? Cards { get; set; }
    public ParticleCards? Kicked { get; set; }
    public List<EvbZone> Zones { get; set; } = [];
}
