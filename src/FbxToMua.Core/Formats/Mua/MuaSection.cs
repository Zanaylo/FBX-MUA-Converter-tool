namespace FbxToMua.Core.Formats.Mua;

public static class MuaSection
{
    public const int Count = 17;
    public const int Skeleton = 0;
    public const int Bone = 1;
    public const int Mesh = 2;
    public const int Part = 3;
    public const int Material = 4;
    public const int Assign = 5;
    public const int Texture = 6;
    public const int UvAnimation = 7;
    public const int AnimationList = 8;
    public const int AnimationKey = 9;
    public const int Script = 11;
    public const int Order = 12;
    public const int Vertex = 13;
    public const int Index = 14;
    public const int StringInfo = 15;
    public const int String = 16;

    public static readonly int[] Stride = [0x20, 0x130, 0xc0, 0x20, 0x50, 0x20, 0x10, 0x1c, 0x10, 0x20, 0x20,
        0x10, 0x20, 0x50, 2, 0x10, 1];
}
