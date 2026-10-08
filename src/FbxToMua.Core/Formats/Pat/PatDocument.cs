namespace FbxToMua.Core.Formats.Pat;

public enum PatBlend
{
    Normal = 0,
    Additive = 1,
    Subtractive = 2,
}

public sealed record PatAtlas(int Id, int Width, int Height, byte[] Dds);

public sealed class PatPart
{
    public int Id { get; set; }
    public int Atlas { get; set; }
    public int U { get; set; }
    public int V { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int PivotX { get; set; }
    public int PivotY { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class PatSprite
{
    public int Part { get; set; } = -1;
    public int X { get; set; }
    public int Y { get; set; }
    public float ZoomX { get; set; } = 1.0f;
    public float ZoomY { get; set; } = 1.0f;
    public uint Tint { get; set; } = 0xffffffffu;
    public int Priority { get; set; }
    public int Blend { get; set; }
    public float Turns { get; set; }
    public int Id { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
}

public sealed class PatPattern
{
    public string Name { get; set; } = string.Empty;
    public List<PatSprite> Sprites { get; } = [];
}

public sealed class PatDocument
{
    public List<PatAtlas> Atlases { get; } = [];
    public Dictionary<int, PatPart> Parts { get; } = [];
    public List<PatPattern> Patterns { get; } = [];

    public PatPattern? Find(string name)
    {
        string wanted = name.Trim(' ', '\t');
        PatPattern? trimmed = null;

        foreach (PatPattern pattern in Patterns)
        {
            if (pattern.Name == name)
                return pattern;

            if (trimmed is null && pattern.Name.Trim(' ', '\t') == wanted)
                trimmed = pattern;
        }

        return trimmed;
    }

    public PatPart? PartOf(int id) => Parts.GetValueOrDefault(id);

    public PatAtlas? AtlasOf(int id) => Atlases.FirstOrDefault(atlas => atlas.Id == id);
}
