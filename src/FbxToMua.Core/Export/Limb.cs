using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

public readonly record struct LimbOrigin(int Node, float Pull)
{
    public const int NoNode = -1;
}

public sealed record Limb(string Name, int Parent, Pose Rest, List<Pose> Poses, LimbOrigin Origin = default);
