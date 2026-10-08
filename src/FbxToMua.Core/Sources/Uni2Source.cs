using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Sources;

public sealed class Uni2Source : IStageSource
{
    private const int HeaderBytes = 64;
    private const int ArchiveNameBytes = 52;
    private const int FolderBytes = 128;
    private const int FolderNameBytes = 116;
    private const int NameOffset = 16;
    private const int MostFolders = 20000;
    private const int MostFiles = 200000;
    private const long MostIndexBytes = 32L * 1024 * 1024;
    private const string StageRoot = "bg";

    private static readonly int[] EntrySizes = [80, 64];

    private sealed record Entry(string Stage, string File, uint Offset, uint Size, string Archive);

    private readonly string _root;
    private readonly List<Entry> _entries = [];

    public GameKind Game => GameKind.Uni2;

    private Uni2Source(string root) => _root = root;

    public static Uni2Source? Open(string folder)
    {
        string root = Path.Combine(folder, "d");

        if (!Directory.Exists(root))
            return null;

        Uni2Source source = new(root);

        foreach (string candidate in Directory.EnumerateFiles(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (new FileInfo(candidate).Length <= MostIndexBytes)
                source.TakeIndex(File.ReadAllBytes(candidate));
        }

        return source._entries.Count > 0 ? source : null;
    }

    public IReadOnlyList<SourceStage> Stages() => StageTally.Compose(_entries.Where(entry => entry.Stage.Length > 0)
        .Select(entry => (entry.Stage, entry.File, entry.Size)), BgList());

    public IReadOnlyList<string> Files(string stage) => _entries.Where(entry => Same(entry.Stage, stage)).Select(entry => entry.File).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public byte[]? Read(string stage, string file)
    {
        Entry? entry = _entries.FirstOrDefault(one => Same(one.Stage, stage) && Same(one.File, file));

        if (entry is null)
            return null;

        using FileStream stream = File.OpenRead(Path.Combine(_root, entry.Archive));

        return UniSource.ReadAt(stream, entry.Offset, (int)entry.Size);
    }

    public string BgList()
    {
        byte[]? list = Read(string.Empty, StageTally.BgListFile);

        return list is null ? string.Empty : LittleEndian.Latin1(list);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static int EntrySizeFor(long bytes, int folders, int files)
    {
        return EntrySizes.FirstOrDefault(size => HeaderBytes + (long)folders * FolderBytes + (long)files * size == bytes);
    }

    private static bool Printable(string text) => text.Length > 0 && text.All(letter => letter >= 32 && letter <= 126);

    private static (string Stage, bool Wanted) Split(string folder)
    {
        string[] parts = folder.Replace('/', '\\').Trim('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        int root = Array.FindLastIndex(parts, part => Same(part, StageRoot));

        if (root < 0)
            return (string.Empty, false);

        return (string.Join('\\', parts[(root + 1)..]), true);
    }

    private void TakeIndex(byte[] data)
    {
        if (data.Length < HeaderBytes)
            return;

        int folders = LittleEndian.I32(data, 0);
        int files = LittleEndian.I32(data, 4);

        if (folders <= 0 || folders > MostFolders || files <= 0 || files > MostFiles)
            return;

        int entryBytes = EntrySizeFor(data.Length, folders, files);
        string archive = LittleEndian.Ascii(data, 12, ArchiveNameBytes);

        if (entryBytes == 0 || !Printable(archive) || !File.Exists(Path.Combine(_root, archive)))
            return;

        List<(string Name, int Count)> folderList = [];
        int offset = HeaderBytes;

        for (int i = 0; i < folders; ++i)
        {
            folderList.Add((LittleEndian.Ascii(data, offset + 12, FolderNameBytes), LittleEndian.I32(data, offset)));
            offset += FolderBytes;
        }

        int entry = 0;

        foreach ((string name, int count) in folderList)
        {
            (string stage, bool wanted) = Split(name);

            for (int k = 0; k < count && entry < files; ++k, ++entry)
            {
                uint size = LittleEndian.U32(data, offset + 8);
                uint at = LittleEndian.U32(data, offset + 12);
                string file = LittleEndian.Ascii(data, offset + NameOffset, entryBytes - NameOffset);
                offset += entryBytes;

                if (wanted && size != 0 && file.Length > 0)
                    _entries.Add(new Entry(stage, file, at, size, archive));
            }
        }
    }
}
