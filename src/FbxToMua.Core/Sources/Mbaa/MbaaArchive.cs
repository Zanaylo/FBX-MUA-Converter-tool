using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Sources.Mbaa;

public sealed record PackedEntry(string Folder, string Name, uint Offset, uint Size, string Archive);

public sealed class MbaaArchive
{
    private const int Header = 0x34;
    private const int FolderName = 0x100;
    private const int FileName = 0x20;
    private const string Magic = "FilePacHeaderA";
    private const int MostFolders = 4096;
    private const int MostFiles = 200000;

    private sealed class Pack
    {
        public string Path { get; init; } = string.Empty;
        public uint Seed { get; init; }
        public uint TableSize { get; init; }
        public byte Step { get; init; }
        public bool Crypted { get; init; }
        public uint BlockBytes { get; init; }
    }

    private readonly Dictionary<string, Pack> _packs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PackedEntry> _entries = [];

    public static MbaaArchive? Open(string folder)
    {
        MbaaArchive archive = new();

        foreach (string path in Directory.EnumerateFiles(folder, "*.p"))
            archive.Load(path);

        return archive._packs.Count > 0 ? archive : null;
    }

    public IEnumerable<PackedEntry> List(string folder) => _entries.Where(entry => string.Equals(entry.Folder, folder, StringComparison.OrdinalIgnoreCase));

    public byte[]? Read(string folder, string name)
    {
        PackedEntry? entry = _entries.FirstOrDefault(one => string.Equals(one.Name, name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(one.Folder, folder, StringComparison.OrdinalIgnoreCase));

        return entry is null ? null : Read(entry);
    }

    private static void Crypt(Span<byte> data, uint seed, byte step, uint skip)
    {
        byte[] key = BitConverter.GetBytes(seed);
        int index = 0;

        for (uint i = 0; i < skip; ++i)
        {
            key[index] = unchecked((byte)(key[index] + step));
            index = (index + 1) & 3;
        }

        for (int i = 0; i < data.Length; ++i)
        {
            data[i] ^= key[index];
            key[index] = unchecked((byte)(key[index] + step));
            index = (index + 1) & 3;
        }
    }

    private static string TakeName(byte[] raw, uint seed, byte step)
    {
        Crypt(raw, seed, step != 0 ? step : (byte)1, 0);

        return LittleEndian.Ascii(raw, 0, raw.Length);
    }

    private void Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] header = new byte[Header];

        if (stream.Read(header) != Header || !LittleEndian.Starts(header, Magic))
            return;

        uint step = LittleEndian.U32(header, 0x2c);
        Pack pack = new()
        {
            Path = path,
            Seed = LittleEndian.U32(header, 0x14),
            TableSize = LittleEndian.U32(header, 0x18),
            Crypted = LittleEndian.U32(header, 0x28) != 0,
            Step = step == 0 ? (byte)1 : (byte)step,
            BlockBytes = LittleEndian.U32(header, 0x30),
        };

        uint folders = LittleEndian.U32(header, 0x20);
        uint files = LittleEndian.U32(header, 0x24);

        if (pack.TableSize <= Header || folders > MostFolders || files > MostFiles)
            return;

        byte[] table = new byte[pack.TableSize - Header];

        if (stream.Read(table) != table.Length)
            return;

        List<PackedEntry>? entries = ReadTable(pack, table, folders, files);

        if (entries is null)
            return;

        _packs[path] = pack;
        _entries.AddRange(entries);
    }

    private static List<PackedEntry>? ReadTable(Pack pack, byte[] table, uint folders, uint files)
    {
        List<(string Name, uint First)> folderList = [];
        int at = 0;

        for (uint i = 0; i < folders; ++i)
        {
            if (at + 12 + FolderName > table.Length)
                return null;

            uint spare = LittleEndian.U32(table, at + 4);
            uint size = LittleEndian.U32(table, at + 8);
            at += 12;
            byte[] raw = table.AsSpan(at, FolderName).ToArray();
            at += FolderName;

            if (size == 0)
                continue;

            folderList.Add((TakeName(raw, pack.Seed, (byte)(size & 0xff)).TrimStart('.', '\\'), spare));
        }

        List<(string Name, uint Offset, uint Size)> list = [];

        for (uint i = 0; i < files; ++i)
        {
            if (at + 12 + FileName > table.Length)
                return null;

            uint offset = LittleEndian.U32(table, at);
            uint size = LittleEndian.U32(table, at + 8);
            at += 12;
            byte[] raw = table.AsSpan(at, FileName).ToArray();
            at += FileName;
            list.Add((TakeName(raw, pack.Seed, (byte)(size & 0xff)), offset, size));
        }

        string[] owner = new string[list.Count];
        Array.Fill(owner, string.Empty);

        for (int i = 0; i < folderList.Count; ++i)
        {
            uint first = folderList[i].First;
            uint last = i + 1 < folderList.Count ? folderList[i + 1].First : (uint)list.Count;

            for (uint f = first; f < last && f < list.Count; ++f)
                owner[f] = folderList[i].Name;
        }

        return list.Select((file, index) => new PackedEntry(owner[index], file.Name, file.Offset, file.Size, pack.Path)).ToList();
    }

    private byte[]? Read(PackedEntry entry)
    {
        if (entry.Size == 0 || !_packs.TryGetValue(entry.Archive, out Pack? pack))
            return null;

        using FileStream stream = File.OpenRead(pack.Path);
        stream.Seek(pack.TableSize + entry.Offset, SeekOrigin.Begin);
        byte[] data = new byte[entry.Size];

        if (stream.ReadAtLeast(data, data.Length, throwOnEndOfStream: false) != data.Length)
            return null;

        if (!pack.Crypted)
            return data;

        int head = (int)Math.Min(data.Length, pack.BlockBytes);
        Crypt(data.AsSpan(0, head), pack.Seed, pack.Step, 0);

        if (data.Length > pack.BlockBytes * 2L)
            Crypt(data.AsSpan((int)(data.Length - pack.BlockBytes), (int)pack.BlockBytes), pack.Seed, pack.Step, pack.BlockBytes);

        return data;
    }
}
