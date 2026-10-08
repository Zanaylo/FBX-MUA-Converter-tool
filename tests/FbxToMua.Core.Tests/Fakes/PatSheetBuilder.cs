using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Pat;

namespace FbxToMua.Core.Tests.Fakes;

public static class PatSheetBuilder
{
    public static PatWriteCutout LeafCut() => new(3, "leaf", 64, 90, 16, 32, 64, 48, 128, 96);

    public static PatWriteAtlas WhiteAtlas(int side = 8) => new("sheet", side, side, Enumerable.Repeat((byte)255, side * side * 4).ToArray());

    public static void Tilt(byte[] blob, IReadOnlyList<(float Pitch, float Yaw)> tilts)
    {
        int next = 0;

        for (int at = 0; at + 20 <= blob.Length && next < tilts.Count; ++at)
        {
            if (!LittleEndian.TagAt(blob, at, "PRA3"))
                continue;

            BitConverter.TryWriteBytes(blob.AsSpan(at + 8), tilts[next].Pitch);
            BitConverter.TryWriteBytes(blob.AsSpan(at + 12), tilts[next].Yaw);
            ++next;
        }
    }
}
