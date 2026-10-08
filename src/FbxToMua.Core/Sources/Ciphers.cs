using System.Security.Cryptography;
using System.Text;
using FbxToMua.Core.Sources.Tables;

namespace FbxToMua.Core.Sources;

public static class MbtlCipher
{
    private const uint Mask = 0x3ff;
    private const uint SeedConstant = 0x76381;

    public static uint Phase(byte[] data)
    {
        if (data.Length < 2)
            return 0;

        uint lead = ((uint)data[0] ^ 0xa5) ^ 0xac;
        uint seed = lead ^ ((uint)data[1] ^ 0x18) ^ SeedConstant;

        return (seed + (uint)data.Length - 1) & Mask;
    }

    public static void DecryptAt(byte[] data, uint phase)
    {
        if (data.Length < 2)
            return;

        data[0] ^= 0xa5;
        data[1] ^= 0x18;
        uint lead = (uint)data[0] ^ 0xac;

        for (int i = 2; i < data.Length; ++i)
            data[i] ^= MbtlKey.Bytes[lead ^ ((phase - (uint)i) & Mask)];
    }
}

public static class UnielCipher
{
    private static readonly byte[] Key = [0xd3, 0x04, 0xf5, 0x27, 0xf3, 0x2e, 0x29, 0x9c, 0x96, 0xf6, 0xfe, 0x4f, 0x47, 0xdd, 0xf4, 0xa9];

    public static void Decrypt(byte[] data)
    {
        if (data.Length == 0)
            return;

        byte[] state = new byte[256];

        for (int i = 0; i < state.Length; ++i)
            state[i] = (byte)i;

        byte mix = 0;

        for (int i = 0; i < state.Length; ++i)
        {
            mix = unchecked((byte)(mix + state[i] + Key[i % Key.Length]));
            (state[i], state[mix]) = (state[mix], state[i]);
        }

        byte step = 0;
        mix = 0;

        for (int i = 0; i < data.Length; ++i)
        {
            step = unchecked((byte)(step + 1));
            mix = unchecked((byte)(mix + state[step]));
            (state[step], state[mix]) = (state[mix], state[step]);
            data[i] ^= state[unchecked((byte)(state[step] + state[mix]))];
        }
    }
}

public enum ArcKey
{
    Bbtag,
    P4u2,
}

public static class ArcCrypt
{
    private const int KeyBytes = 43;

    private static readonly byte[][] Keys =
    [
        [
            0xf5, 0x5c, 0x84, 0x2a, 0xad, 0x61, 0x54, 0xe7, 0x0a, 0xfc, 0x99, 0x6b, 0xd5, 0xa4, 0xd3, 0xd8,
            0x48, 0x26, 0x69, 0xcb, 0x07, 0x42, 0x13, 0x5e, 0x10, 0x23, 0xd2, 0x6d, 0x36, 0xc7, 0xc1, 0x66,
            0xdf, 0xa1, 0xad, 0xf1, 0x44, 0x44, 0x7e, 0xc9, 0x8e, 0x24, 0x99,
        ],
        [
            0x71, 0x59, 0x7a, 0xba, 0x10, 0x22, 0xbf, 0xba, 0xc5, 0x04, 0x08, 0x9d, 0x73, 0x90, 0xb7, 0xfe,
            0x29, 0x95, 0xff, 0xe0, 0x6a, 0x01, 0x3f, 0xfb, 0xb9, 0x3a, 0x2c, 0x6e, 0xec, 0x13, 0x96, 0xaf,
            0xff, 0xeb, 0xa4, 0x73, 0xd3, 0x4e, 0x52, 0x19, 0xe8, 0xad, 0x27,
        ],
    ];

    public static string Md5(string text) => Convert.ToHexStringLower(MD5.HashData(Encoding.Latin1.GetBytes(text.ToLowerInvariant())));

    public static string NameOf(string relative) => Md5(relative.ToLowerInvariant().Replace('\\', '/'));

    public static void Apply(ArcKey key, string relative, byte[] data)
    {
        byte[] bytes = Keys[(int)key];
        byte[] digest = MD5.HashData(Encoding.Latin1.GetBytes(NameOf(relative)));
        int index = digest[7] % KeyBytes;

        for (int i = 0; i < data.Length; ++i)
        {
            data[i] ^= bytes[index];
            index = (index + 1) % KeyBytes;
        }
    }
}
