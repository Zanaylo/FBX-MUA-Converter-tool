using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Sources;

public sealed class UniSource : IStageSource
{
    private const int Header = 64;
    private const int FolderRecord = 128;
    private const int FileRecord = 64;
    private const uint MostFolders = 20000;
    private const uint MostFiles = 500000;
    private const string StageRoot = "bg";

    private sealed record Entry(string Stage, string File, uint Offset, uint Size);

    private readonly List<Entry> _entries = [];
    private FileStream? _data;

    public GameKind Game { get; }

    private UniSource(GameKind game) => Game = game;

    public static UniSource? Open(string folder, GameKind game)
    {
        string root = Path.Combine(folder, "d");

        if (!Directory.Exists(root))
            return null;

        UniSource source = new(game);

        foreach (string candidate in Directory.EnumerateFiles(root))
        {
            if (source.TakeListing(root, candidate))
                return source;
        }

        source.Dispose();
        return null;
    }

    public IReadOnlyList<SourceStage> Stages() => StageTally.Compose(_entries.Where(entry => entry.Stage.Length > 0)
        .Select(entry => (entry.Stage, entry.File, entry.Size)), BgList());

    public IReadOnlyList<string> Files(string stage) => _entries.Where(entry => Same(entry.Stage, stage)).Select(entry => entry.File).ToList();

    public byte[]? Read(string stage, string file)
    {
        Entry? entry = _entries.FirstOrDefault(one => Same(one.Stage, stage) && Same(one.File, file));

        return entry is null || _data is null ? null : ReadAt(_data, entry.Offset, (int)entry.Size);
    }

    public string BgList()
    {
        byte[]? list = Read(string.Empty, StageTally.BgListFile);

        return list is null ? string.Empty : LittleEndian.Latin1(list);
    }

    public void Dispose() => _data?.Dispose();

    internal static byte[]? ReadAt(FileStream stream, long offset, int size)
    {
        if (size <= 0)
            return null;

        byte[] data = new byte[size];
        stream.Seek(offset, SeekOrigin.Begin);
        int read = stream.ReadAtLeast(data, size, throwOnEndOfStream: false);

        return read == 0 ? null : read == size ? data : data[..read];
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static byte[]? ReadIndex(string path)
    {
        FileInfo info = new(path);

        if (info.Length < Header)
            return null;

        using FileStream stream = File.OpenRead(path);
        byte[] head = new byte[Header];
        stream.ReadExactly(head);
        uint folders = LittleEndian.U32(head, 0);
        uint files = LittleEndian.U32(head, 4);

        if (folders == 0 || folders >= MostFolders || files == 0 || files >= MostFiles)
            return null;

        long expected = Header + (long)folders * FolderRecord + (long)files * FileRecord;

        return info.Length == expected ? File.ReadAllBytes(path) : null;
    }

    private bool TakeListing(string root, string path)
    {
        byte[]? blob = ReadIndex(path);

        if (blob is null)
            return false;

        string archive = LittleEndian.Ascii(blob, 12, Header - 12);
        string archivePath = Path.Combine(root, archive);

        if (archive.Length == 0 || !File.Exists(archivePath))
            return false;

        List<Entry> entries = Entries(blob);

        if (entries.Count == 0)
            return false;

        _entries.AddRange(entries);
        _data = File.OpenRead(archivePath);

        return true;
    }

    private static List<Entry> Entries(byte[] blob)
    {
        uint folders = LittleEndian.U32(blob, 0);
        uint files = LittleEndian.U32(blob, 4);
        List<Entry> entries = [];
        long at = Header;
        long taken = 0;

        for (uint folder = 0; folder < folders; ++folder)
        {
            uint count = LittleEndian.U32(blob, at);
            string path = LittleEndian.Ascii(blob, at + 12, FolderRecord - 16).Replace('/', '\\').TrimEnd('\\');
            at += FolderRecord;

            int separator = path.IndexOf('\\');
            bool wanted = string.Equals(separator < 0 ? path : path[..separator], StageRoot, StringComparison.OrdinalIgnoreCase);

            for (uint i = 0; i < count && taken < files; ++i, ++taken)
            {
                long record = Header + (long)folders * FolderRecord + taken * FileRecord;

                if (!wanted)
                    continue;

                string file = LittleEndian.Ascii(blob, record + 16, FileRecord - 16);
                uint size = LittleEndian.U32(blob, record + 4);

                if (file.Length > 0 && size > 0)
                    entries.Add(new Entry(separator < 0 ? string.Empty : path[(separator + 1)..], file, LittleEndian.U32(blob, record + 12), size));
            }
        }

        return entries;
    }
}
