using System.Buffers.Binary;

namespace FbxToMua.Core.Binary;

public static class LittleEndian
{
    public static uint U32(ReadOnlySpan<byte> blob, long at)
    {
        if (at < 0 || at + 4 > blob.Length)
            return 0;

        return BinaryPrimitives.ReadUInt32LittleEndian(blob[(int)at..]);
    }

    public static int I32(ReadOnlySpan<byte> blob, long at) => unchecked((int)U32(blob, at));

    public static ushort U16(ReadOnlySpan<byte> blob, long at)
    {
        if (at < 0 || at + 2 > blob.Length)
            return 0;

        return BinaryPrimitives.ReadUInt16LittleEndian(blob[(int)at..]);
    }

    public static float F32(ReadOnlySpan<byte> blob, long at)
    {
        if (at < 0 || at + 4 > blob.Length)
            return 0.0f;

        return BinaryPrimitives.ReadSingleLittleEndian(blob[(int)at..]);
    }

    public static void Put(Span<byte> blob, long at, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(blob[(int)at..], value);
    }

    public static bool Starts(ReadOnlySpan<byte> blob, string magic)
    {
        if (blob.Length < magic.Length)
            return false;

        for (int i = 0; i < magic.Length; ++i)
        {
            if (blob[i] != (byte)magic[i])
                return false;
        }

        return true;
    }

    public static bool TagAt(ReadOnlySpan<byte> blob, long at, string tag)
    {
        return at >= 0 && at + tag.Length <= blob.Length && Starts(blob[(int)at..], tag);
    }

    public static string Ascii(ReadOnlySpan<byte> blob, long at, int length)
    {
        if (at < 0 || at >= blob.Length)
            return string.Empty;

        ReadOnlySpan<byte> field = blob.Slice((int)at, (int)Math.Min(length, blob.Length - at));
        int end = field.IndexOf((byte)0);

        return Latin1(end < 0 ? field : field[..end]);
    }

    public static string Latin1(ReadOnlySpan<byte> bytes) => System.Text.Encoding.Latin1.GetString(bytes);

    public static byte[] Latin1(string text) => System.Text.Encoding.Latin1.GetBytes(text);
}
