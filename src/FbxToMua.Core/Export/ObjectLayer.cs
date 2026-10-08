using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Formats.Objects;
using FbxToMua.Core.Formats.Pat;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public readonly record struct LayerCorner(Float3 Position, float U, float V);

public sealed class LayerSprite
{
    public int Atlas { get; set; }
    public LayerCorner[] Corners { get; set; } = new LayerCorner[4];
    public MuaColour Colour { get; set; }
    public Pose Rest;
    public List<Pose> Poses { get; } = [];
}

public sealed class LayerGroup
{
    public int Entry { get; init; }
    public int Prio { get; init; }
    public int Blend { get; init; }
    public int Span { get; init; }
    public bool Moves { get; set; }
    public Pose Frame;
    public List<LayerSprite> Sprites { get; } = [];
}

public sealed record LayerImage(string Name, byte[] Data);

public sealed class ObjectLayer
{
    public const int FrontPrio = 400;
    public const int MostSprites = 12;

    public List<LayerGroup> Groups { get; } = [];
    public List<LayerImage> Atlases { get; } = [];
    public List<string> Missing { get; } = [];
    public int Sprites { get; set; }
    public int Front { get; set; }

    public static ObjectLayer Convert(IReadOnlyList<ObjectEntry> entries, PatDocument sheet, string stem)
    {
        ObjectLayer layer = new();
        ObjectLayerConverter converter = new(sheet, stem, layer);

        foreach (ObjectEntry entry in entries)
            converter.Add(entry);

        return layer;
    }
}
