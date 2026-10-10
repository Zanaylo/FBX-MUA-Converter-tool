using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public struct Framing
{
    private const float NeutralScale = 10.0f;

    public Float3 Scale;
    public Float3 Position;
    public float Tilt;
    public float Turn;

    public static Framing Neutral => new() { Scale = Float3.All(NeutralScale) };

    public readonly Matrix Placement()
    {
        float plane = (float)BattleCamera.FightPlane();
        Matrix place = Matrix.Identity;
        place[0] = plane * Scale[0];
        place[5] = plane * Scale[1];
        place[10] = -plane * Scale[2];
        place[12] = plane * Position[0] / (float)BattleCamera.Aspect;
        place[13] = plane * Position[1];
        place[14] = plane * Position[2];

        return place;
    }
}

public sealed record ExportFile(string Name, byte[] Data);

public sealed record ExportSource
{
    public byte[] Model { get; init; } = [];
    public Func<string, byte[]?> Image { get; init; } = _ => null;
    public Framing Framing { get; init; } = Framing.Neutral;
    public Reframe Reframe { get; init; } = Reframe.None;
    public string Stage { get; init; } = string.Empty;
    public byte[] Objects { get; init; } = [];
    public byte[] Sheet { get; init; } = [];
    public string SheetName { get; init; } = string.Empty;
}

public readonly record struct PulledNode(int Node, float Factor);

public sealed class ExportResult
{
    public string Stage { get; init; } = string.Empty;
    public byte[] Model { get; set; } = [];
    public byte[] Bare { get; set; } = [];
    public List<ExportFile> Motions { get; } = [];
    public List<ExportFile> Scripts { get; } = [];
    public List<ExportFile> Images { get; } = [];
    public List<string> Missing { get; } = [];
    public List<string> Foreign { get; } = [];
    public List<string> Absent { get; } = [];
    public List<PulledNode> Pulls { get; } = [];
    public int Meshes { get; set; }
    public int Animated { get; set; }
    public int Sprites { get; set; }
    public int Front { get; set; }
    public int Pulled { get; set; }
    public int Tilt { get; set; }
    public bool Turned { get; set; }
}

public sealed record ExportArchives(byte[] Scene, byte[] Geometry, byte[] Art);
