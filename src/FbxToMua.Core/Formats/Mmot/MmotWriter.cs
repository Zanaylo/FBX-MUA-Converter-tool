using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Formats.Mmot;

public static class MmotWriter
{
    private const uint Version = 0x3eb;
    private const int Sections = 10;
    private const int Table = 0x20;
    private const int TableStride = 0x10;
    private const int Data = Table + Sections * TableStride;

    private const int HeaderSection = 0;
    private const int BoneSection = 1;
    private const int KeyListSection = 2;
    private const int KeySection = 3;
    private const int LinkedSection = 4;
    private const int LinkOrderSection = 5;
    private const int LinkSpareSection = 6;
    private const int SpareSection = 7;
    private const int StringInfoSection = 8;
    private const int StringSection = 9;

    private const int BoneStride = 0xe0;
    private const int ListStride = 0x10;
    private const int KeyStride = 0x20;
    private const int LinkStride = 0x20;
    private const int InfoStride = 0x10;
    private const uint FirstMeshString = 2;

    public static byte[] Build(MmotTake take)
    {
        List<string> strings = [take.Name, take.Root, .. take.Meshes];
        ByteSink body = new();
        uint[] offsets = new uint[Sections];
        uint[] counts = new uint[Sections];

        void Mark(int section, int count)
        {
            offsets[section] = (uint)(Data + body.Size);
            counts[section] = (uint)count;
        }

        Mark(BoneSection, take.Bones.Count);
        PutBones(body, take);

        Mark(KeyListSection, take.Bones.Count * MmotBone.Tracks);
        uint keys = PutKeyLists(body, take);

        Mark(KeySection, (int)keys);
        PutKeys(body, take);

        Mark(LinkedSection, take.Meshes.Count);

        for (int i = 0; i < take.Meshes.Count; ++i)
            body.Record(LinkStride, 0, FirstMeshString + (uint)i, (uint)i, 1);

        Mark(LinkOrderSection, take.Meshes.Count);

        for (int i = 0; i < take.Meshes.Count; ++i)
            body.Record(LinkStride, (uint)i);

        Mark(LinkSpareSection, take.Meshes.Count);

        for (int i = 0; i < take.Meshes.Count; ++i)
            body.Record(LinkStride);

        Mark(SpareSection, 0);
        Mark(StringInfoSection, strings.Count);
        uint offset = 0;

        foreach (string text in strings)
        {
            body.Record(InfoStride, offset, (uint)text.Length);
            offset += (uint)text.Length + 1;
        }

        Mark(StringSection, (int)offset);

        foreach (string text in strings)
        {
            body.Text(text);
            body.Byte(0);
        }

        offsets[HeaderSection] = 0;
        counts[HeaderSection] = 1;

        ByteSink head = new();
        head.Text("MMOT");
        head.Dword(Version);
        head.PadTo(Table);

        for (int i = 0; i < Sections; ++i)
            head.Record(TableStride, offsets[i], counts[i]);

        head.Bytes(body.ToArray());

        return head.ToArray();
    }

    private static void PutBones(ByteSink body, MmotTake take)
    {
        uint firstList = 0;

        foreach (MmotBone bone in take.Bones)
        {
            int start = body.Size;
            body.Floats(bone.Local);
            body.Floats(bone.Unbind);
            body.Floats(bone.ParentUnbind);
            body.Int(take.Frames);

            for (uint k = 0; k < MmotBone.Tracks; ++k)
                body.Dword(firstList + k);

            body.Pad(start, BoneStride);
            firstList += MmotBone.Tracks;
        }
    }

    private static uint PutKeyLists(ByteSink body, MmotTake take)
    {
        uint firstKey = 0;

        foreach (MmotBone bone in take.Bones)
        {
            foreach (List<MmotKey> track in bone.Track)
            {
                body.Record(ListStride, firstKey, (uint)track.Count);
                firstKey += (uint)track.Count;
            }
        }

        return firstKey;
    }

    private static void PutKeys(ByteSink body, MmotTake take)
    {
        foreach (MmotBone bone in take.Bones)
        {
            foreach (List<MmotKey> track in bone.Track)
            {
                foreach (MmotKey key in track)
                {
                    int start = body.Size;
                    body.Floats(key.Value);
                    body.Int(key.Frame);
                    body.Pad(start, KeyStride);
                }
            }
        }
    }
}
