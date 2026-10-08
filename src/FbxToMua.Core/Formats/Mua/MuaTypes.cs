using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.Mua;

public enum MuaBlend : uint
{
    Add = 2,
    Subtract = 4,
    Unset = 0x7fffffff,
}

public static class MuaFlags
{
    public const uint Scene = 0x10000100;
    public const uint Static = 0x20000100;
    public const uint Animated = 0x10000140;
    public const uint BothFaces = 0x400;
    public const uint NoDepthTest = 0x1000;
    public const uint NoDepthWrite = 0x2000;
}

public readonly record struct MuaColour(byte R, byte G, byte B, byte A);

public readonly record struct MuaPart(int Material, int FirstIndex, int Indices);

public readonly record struct MuaSkeleton(int FirstBone, int Bones, int Script, MuaBlend Blend, uint Flags)
{
    public const int NoScript = -1;
}

public readonly record struct MuaTriangle(int A, int B, int C);

public struct MuaVertex
{
    public const int MostPerMesh = 65535;

    public Float3 Position;
    public Float3 Normal;
    public Float3 Tangent;
    public float U;
    public float V;
    public MuaColour Colour;
    public Float3 BoneIndex;
    public Float3 Weight;

    public static MuaVertex Unrigged()
    {
        return new MuaVertex { BoneIndex = Float3.All(-1.0f), Weight = Float3.All(-1.0f) };
    }

    public readonly MuaVertex RiggedTo(int bone)
    {
        MuaVertex rigged = this;
        rigged.BoneIndex = Float3.All(-1.0f);
        rigged.Weight = Float3.All(-1.0f);
        rigged.BoneIndex[0] = bone;
        rigged.Weight[0] = 1.0f;

        return rigged;
    }
}

public sealed class MuaMesh
{
    public string Name { get; set; } = string.Empty;
    public int Skeleton { get; set; }
    public int Partner { get; set; }
    public int FirstPart { get; set; }
    public int Parts { get; set; }
    public int FirstVertex { get; set; }
    public int Vertices { get; set; }
    public Float3 Pivot;
}

public sealed class MuaModel
{
    public List<string> Textures { get; } = [];
    public List<int> Materials { get; } = [];
    public List<string> Scripts { get; } = [];
    public List<MuaBone> Bones { get; } = [];
    public List<MuaSkeleton> Skeletons { get; } = [];
    public List<MuaMesh> Meshes { get; } = [];
    public List<MuaPart> Parts { get; } = [];
    public List<MuaVertex> Vertices { get; } = [];
    public List<ushort> Indices { get; } = [];
}
