using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Formats.Dds;

public static class DdsHeader
{
    public const int HeaderBytes = 128;
    private const uint HeaderSize = 124;
    private const uint RequiredFlags = 0x1 | 0x2 | 0x4 | 0x1000;
    private const uint PitchFlag = 0x8;
    private const uint LinearSizeFlag = 0x80000;
    private const uint FourCcFlag = 0x4;
    private const uint SmallestLevel = 4;
    private const uint BitsPerByte = 8;
    private const uint BlockSide = 4;
    private const uint SmallBlockBytes = 8;
    private const uint LargeBlockBytes = 16;

    private const int SizeAt = 4;
    private const int FlagsAt = 8;
    private const int HeightAt = 12;
    private const int WidthAt = 16;
    private const int PitchAt = 20;
    private const int FormatFlagsAt = 80;
    private const int FourCcAt = 84;
    private const int BitsAt = 88;

    public static bool IsDds(byte[] blob) => LittleEndian.Starts(blob, "DDS ");

    public static uint FirstLevelBytes(byte[] dds)
    {
        if (!Parsed(dds))
            return 0;

        uint height = LittleEndian.U32(dds, HeightAt);

        if (Pitched(dds))
            return LittleEndian.U32(dds, PitchAt) * height;

        uint bits = LittleEndian.U32(dds, BitsAt);

        if (bits == 0)
            return LittleEndian.U32(dds, PitchAt);

        return Math.Max(SmallestLevel, bits / BitsPerByte * LittleEndian.U32(dds, WidthAt) * height);
    }

    public static void StateLinearSize(byte[] dds)
    {
        if (!Parsed(dds) || Pitched(dds) || LittleEndian.U32(dds, BitsAt) != 0)
            return;

        uint blockBytes = BlockBytes(dds);

        if (blockBytes == 0)
            return;

        LittleEndian.Put(dds, FlagsAt, LittleEndian.U32(dds, FlagsAt) | LinearSizeFlag);
        LittleEndian.Put(dds, PitchAt, Blocks(LittleEndian.U32(dds, WidthAt)) * Blocks(LittleEndian.U32(dds, HeightAt)) * blockBytes);
    }

    public static byte[] WhiteDxt1()
    {
        byte[] dds = new byte[HeaderBytes + SmallBlockBytes];
        LittleEndian.Latin1("DDS ").CopyTo(dds, 0);
        LittleEndian.Put(dds, SizeAt, HeaderSize);
        LittleEndian.Put(dds, FlagsAt, RequiredFlags | LinearSizeFlag);
        LittleEndian.Put(dds, HeightAt, BlockSide);
        LittleEndian.Put(dds, WidthAt, BlockSide);
        LittleEndian.Put(dds, PitchAt, SmallBlockBytes);
        LittleEndian.Put(dds, 76, 32);
        LittleEndian.Put(dds, FormatFlagsAt, FourCcFlag);
        LittleEndian.Latin1("DXT1").CopyTo(dds, FourCcAt);
        LittleEndian.Put(dds, 108, 0x1000);

        for (int i = 0; i < 4; ++i)
            dds[HeaderBytes + i] = 0xff;

        return dds;
    }

    private static bool Parsed(byte[] dds)
    {
        return dds.Length >= HeaderBytes && IsDds(dds) && LittleEndian.U32(dds, SizeAt) == HeaderSize
            && (LittleEndian.U32(dds, FlagsAt) & RequiredFlags) == RequiredFlags;
    }

    private static bool Pitched(byte[] dds) => (LittleEndian.U32(dds, FlagsAt) & PitchFlag) != 0;

    private static uint BlockBytes(byte[] dds)
    {
        if ((LittleEndian.U32(dds, FormatFlagsAt) & FourCcFlag) == 0)
            return 0;

        if (LittleEndian.TagAt(dds, FourCcAt, "DXT1"))
            return SmallBlockBytes;

        return LittleEndian.TagAt(dds, FourCcAt, "DXT") ? LargeBlockBytes : 0;
    }

    private static uint Blocks(uint side) => Math.Max(1u, (side + BlockSide - 1) / BlockSide);
}
