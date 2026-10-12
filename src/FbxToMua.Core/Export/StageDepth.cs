namespace FbxToMua.Core.Export;

public static class StageDepth
{
    public const float Near = 1.0f;
    private const float StageEye = 10.0f;
    private const int Depth = 2;

    public static float Of(Framing framing, float z) => StageEye - (framing.Scale[Depth] * z + framing.Position[Depth]);
}
