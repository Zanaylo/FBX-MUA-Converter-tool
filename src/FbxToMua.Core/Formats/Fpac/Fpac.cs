using System.IO.Compression;
using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Formats.Fpac;

public static class Fpac
{
    private const int Header = 0x20;
    private const int EntryTail = 12;
    private const int StrideSlack = 16;
    private const int PackedHeader = 16;
    private const int ZlibHeader = 2;
    private const int LongestName = 256;
    private const int DeepestNesting = 8;
    private const uint Flags = 1;

    private readonly record struct Layout(int Start, int Count, int NameBytes, int Stride);

    public static bool IsArchive(ReadOnlySpan<byte> blob) => blob.Length >= Header && LittleEndian.Starts(blob, "FPAC");

    public static byte[] Build(IReadOnlyList<FpacEntry> entries)
    {
        int longest = entries.Count == 0 ? 0 : entries.Max(entry => entry.Name.Length);
        int nameBytes = Align(longest + 1, 4);
        int stride = Align(nameBytes + StrideSlack, 16);
        int dataStart = Header + entries.Count * stride;
        int total = dataStart + entries.Sum(entry => Align(entry.Data.Length, 16));

        ByteSink sink = new();
        sink.Text("FPAC");
        sink.Int(dataStart);
        sink.Int(total);
        sink.Int(entries.Count);
        sink.Dword(Flags);
        sink.Int(nameBytes);
        sink.PadTo(Header);

        int offset = 0;

        for (int i = 0; i < entries.Count; ++i)
        {
            int start = sink.Size;
            sink.Text(entries[i].Name);
            sink.PadTo(start + nameBytes);
            sink.Int(i);
            sink.Int(offset);
            sink.Int(entries[i].Data.Length);
            sink.PadTo(start + stride);
            offset += Align(entries[i].Data.Length, 16);
        }

        foreach (FpacEntry entry in entries)
        {
            sink.Bytes(entry.Data);
            sink.PadTo(Align(sink.Size, 16));
        }

        return sink.ToArray();
    }

    public static byte[] Pack(byte[] archive)
    {
        byte[] stream = Zlib(archive);
        ByteSink sink = new();
        sink.Text("DFAS");
        sink.Bytes(archive.AsSpan(0, 4));
        sink.Int(archive.Length);
        sink.Int(stream.Length);
        sink.Bytes(stream);

        return sink.ToArray();
    }

    public static bool TryUnpack(byte[] blob, out byte[] plain)
    {
        plain = [];

        if (blob.Length < PackedHeader + ZlibHeader || !LittleEndian.Starts(blob, "DFAS"))
            return false;

        int size = LittleEndian.I32(blob, 8);

        try
        {
            using MemoryStream source = new(blob, PackedHeader + ZlibHeader, blob.Length - PackedHeader - ZlibHeader);
            using DeflateStream inflater = new(source, CompressionMode.Decompress);
            using MemoryStream target = new(Math.Max(size, 0));
            inflater.CopyTo(target);
            plain = target.ToArray();

            return plain.Length == size;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static byte[] Plain(byte[] blob) => TryUnpack(blob, out byte[] plain) ? plain : blob;

    public static SortedDictionary<string, byte[]> Walk(byte[] blob)
    {
        SortedDictionary<string, byte[]> files = new(StringComparer.Ordinal);
        Gather(Plain(blob), string.Empty, 0, files);

        return files;
    }

    public static IReadOnlyList<string> Names(byte[] blob)
    {
        byte[] archive = Plain(blob);

        if (!TryLayout(archive, out Layout layout))
            return [];

        List<string> names = [];

        for (int i = 0; i < layout.Count; ++i)
        {
            int at = Header + i * layout.Stride;

            if (at + layout.NameBytes > archive.Length)
                return [];

            names.Add(LittleEndian.Ascii(archive, at, layout.NameBytes));
        }

        return names;
    }

    public static byte[]? Ending(IReadOnlyDictionary<string, byte[]> files, string tail)
    {
        foreach (KeyValuePair<string, byte[]> file in files)
        {
            if (file.Key.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
                return file.Value;
        }

        return null;
    }

    public static byte[]? Named(IReadOnlyDictionary<string, byte[]> files, string leaf)
    {
        foreach (KeyValuePair<string, byte[]> file in files)
        {
            if (string.Equals(LeafOf(file.Key), leaf, StringComparison.OrdinalIgnoreCase))
                return file.Value;
        }

        return null;
    }

    private static bool Gather(byte[] blob, string prefix, int depth, SortedDictionary<string, byte[]> files)
    {
        if (depth > DeepestNesting || !TryLayout(blob, out Layout layout))
            return false;

        for (int i = 0; i < layout.Count; ++i)
        {
            int at = Header + i * layout.Stride;

            if (at + layout.NameBytes + EntryTail > blob.Length)
                return false;

            long offset = LittleEndian.U32(blob, at + layout.NameBytes + 4);
            long size = LittleEndian.U32(blob, at + layout.NameBytes + 8);

            if (layout.Start + offset + size > blob.Length)
                return false;

            string name = LittleEndian.Ascii(blob, at, layout.NameBytes);
            byte[] body = Plain(blob.AsSpan((int)(layout.Start + offset), (int)size).ToArray());

            if (IsArchive(body) && Gather(body, prefix + Stem(name) + "/", depth + 1, files))
                continue;

            files[prefix + name] = body;
        }

        return true;
    }

    private static bool TryLayout(byte[] blob, out Layout layout)
    {
        layout = default;

        if (!IsArchive(blob))
            return false;

        int start = (int)LittleEndian.U32(blob, 4);
        int count = (int)LittleEndian.U32(blob, 12);
        int nameBytes = (int)LittleEndian.U32(blob, 20);

        if (count <= 0 || nameBytes <= 0 || nameBytes > LongestName)
            return false;

        int stride = Align(nameBytes + EntryTail, 16);

        if (start > Header && (start - Header) / count >= nameBytes + EntryTail)
            stride = (start - Header) / count;

        layout = new Layout(start, count, nameBytes, stride);
        return true;
    }

    private static byte[] Zlib(byte[] data)
    {
        using MemoryStream target = new();

        using (ZLibStream deflater = new(target, CompressionLevel.Optimal, leaveOpen: true))
            deflater.Write(data);

        return target.ToArray();
    }

    private static int Align(int value, int to) => (value + to - 1) / to * to;

    private static string Stem(string name)
    {
        int dot = name.LastIndexOf('.');

        return dot < 0 ? name : name[..dot];
    }

    private static string LeafOf(string path)
    {
        int slash = path.LastIndexOfAny(['/', '\\']);

        return slash < 0 ? path : path[(slash + 1)..];
    }
}
