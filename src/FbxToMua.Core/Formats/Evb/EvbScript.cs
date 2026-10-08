namespace FbxToMua.Core.Formats.Evb;

public static class EvbCode
{
    public const uint Begin = 0x01;
    public const uint Yield = 0x02;
    public const uint Wait = 0x03;
    public const uint Close = 0x04;
    public const uint Group = 0x05;
    public const uint Pick = 0x06;
    public const uint Spawn = 0x07;
    public const uint Label = 0x09;
    public const uint End = 0x0a;
    public const uint Rect = 0x0b;
    public const uint Jump = 0x0f;
    public const uint Ramp = 0x12;
    public const uint Random = 0x13;
    public const uint RandomEnd = 0x14;
    public const uint Pause = 0x15;
    public const uint SceneOpen = 0x1a;
    public const uint SceneClose = 0x1b;
    public const uint SceneVector = 0x1d;
    public const uint ScenePair = 0x1e;
    public const uint SceneHeight = 0x1f;
    public const uint SceneSwitches = 0x21;
    public const uint ZoneCount = 0x25;
    public const uint Zone = 0x26;
    public const uint SceneTilt = 0x28;
    public const uint None = 0xffffffffu;
}

public sealed record EvbRecord(uint Code, params int[] Operands);

public sealed record EvbScript(IReadOnlyList<string> Sheets, IReadOnlyList<string> Names, IReadOnlyList<EvbRecord> Records);
