using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Import;

public sealed class ArcStageInput
{
    public byte[] Geometry { get; init; } = [];
    public byte[] Scene { get; init; } = [];
    public byte[] Art { get; init; } = [];
    public byte[] Particles { get; init; } = [];
    public byte[] ParticleArt { get; init; } = [];
    public string Stage { get; init; } = string.Empty;
    public GameKind Game { get; init; } = GameKind.Bbtag;
}

public sealed class ArcStageResult
{
    public byte[] Model { get; set; } = [];
    public SortedDictionary<string, byte[]> Images { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, byte[]> Layer { get; } = new(StringComparer.Ordinal);
    public List<float> Flow { get; } = [];
    public List<EvbLamp> Lamps { get; } = [];
    public List<EvbFlip> Flips { get; } = [];
    public List<int> Once { get; } = [];
    public List<int> Kick { get; } = [];
    public bool Fading { get; set; }
    public float Tilt { get; set; }
}
