using FbxToMua.Core.Binary;
using FbxToMua.Core.Sources.Tables;

namespace FbxToMua.Core.Sources;

public sealed class MbtlSource : IStageSource
{
    private const string Archive = "data006.bin";
    private const int TextSlack = 8192;
    private const int ScoreSpan = 2048;
    private const uint PhaseCount = 0x400;
    private const int GoodText = 990;

    private static readonly (string Extension, byte[] Bytes)[] Magics =
    [
        ("dds", [(byte)'D', (byte)'D', (byte)'S', (byte)' ', 0x7c, 0, 0, 0]),
        ("bin", [(byte)'f', (byte)'b', (byte)'x', (byte)'e', (byte)'x', 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
        ("pat", LittleEndian.Latin1("PAniDataFile")),
        ("img", [0, 0, 0, 0, 7, 0, 0, 0]),
    ];

    private readonly FileStream _handle;

    public GameKind Game => GameKind.Mbtl;

    private MbtlSource(FileStream handle) => _handle = handle;

    public static MbtlSource? Open(string folder)
    {
        string path = Path.Combine(folder, Archive);

        return File.Exists(path) ? new MbtlSource(File.OpenRead(path)) : null;
    }

    public IReadOnlyList<SourceStage> Stages()
    {
        IEnumerable<(string, string, uint)> files = MbtlStageIndex.Entries
            .Where(entry => entry.Name.Contains('/'))
            .Select(entry => (entry.Name[..entry.Name.IndexOf('/')], entry.Name[(entry.Name.IndexOf('/') + 1)..], entry.Size));

        return StageTally.Compose(files, BgList());
    }

    public IReadOnlyList<string> Files(string stage)
    {
        string prefix = stage + "/";

        return MbtlStageIndex.Entries
            .Where(entry => entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Name[prefix.Length..]).ToList();
    }

    public byte[]? Read(string stage, string file)
    {
        string key = stage.Length == 0 ? file : $"{stage}/{file}";
        int index = Array.FindIndex(MbtlStageIndex.Entries, entry => string.Equals(entry.Name, key, StringComparison.OrdinalIgnoreCase));

        return index < 0 ? null : Take(MbtlStageIndex.Entries[index].Offset, MbtlStageIndex.Entries[index].Size, file);
    }

    public string BgList()
    {
        byte[]? list = Read(string.Empty, StageTally.BgListFile);

        return list is null ? string.Empty : LittleEndian.Latin1(list);
    }

    public void Dispose() => _handle.Dispose();

    public static bool IsText(string file) => Path.GetExtension(file).ToLowerInvariant() is ".txt" or ".ini" or ".csv";

    public static int TextScore(byte[] data)
    {
        int span = Math.Min(data.Length, ScoreSpan);

        if (span == 0)
            return 0;

        int good = 0;

        for (int i = 0; i < span; ++i)
            good += TextByte(data[i]) ? 1 : 0;

        return good * 1000 / span;
    }

    public static bool MagicOk(string file, byte[] data)
    {
        if (data.Length == 0)
            return false;

        byte[]? magic = MagicFor(file);

        if (magic is not null)
            return data.AsSpan().StartsWith(magic);

        return !IsText(file) || TextScore(data) > GoodText;
    }

    private static bool TextByte(byte value) => value is (byte)'\t' or (byte)'\r' or (byte)'\n' or (>= 0x20 and < 0x7f) or (>= 0x80 and <= 0xfc);

    private static byte[]? MagicFor(string file)
    {
        string extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();

        return Magics.FirstOrDefault(magic => magic.Extension == extension).Bytes;
    }

    private byte[]? Take(uint offset, uint size, string file)
    {
        bool text = IsText(file);
        byte[]? raw = UniSource.ReadAt(_handle, offset, (int)size + (text ? TextSlack : 0));

        if (raw is null)
            return null;

        byte[]? magic = MagicFor(file);
        uint first = MbtlCipher.Phase(raw);

        if (magic is null && !text)
        {
            MbtlCipher.DecryptAt(raw, first);
            return raw;
        }

        uint? phase = BestPhase(raw, magic, first);

        if (phase is null)
            return null;

        MbtlCipher.DecryptAt(raw, phase.Value);

        return text ? TrimToText(raw) : raw;
    }

    private static uint? BestPhase(byte[] raw, byte[]? magic, uint first)
    {
        int span = magic?.Length ?? ScoreSpan;
        byte[] head = raw.AsSpan(0, Math.Min(span, raw.Length)).ToArray();
        int bestScore = 0;
        uint? best = null;

        for (uint step = 0; step < PhaseCount; ++step)
        {
            uint phase = (first + step) & (PhaseCount - 1);
            byte[] probe = (byte[])head.Clone();
            MbtlCipher.DecryptAt(probe, phase);

            if (magic is not null)
            {
                if (probe.AsSpan().StartsWith(magic))
                    return phase;

                continue;
            }

            int score = TextScore(probe);

            if (score <= bestScore)
                continue;

            bestScore = score;
            best = phase;
        }

        return best;
    }

    private static byte[]? TrimToText(byte[] data)
    {
        int end = Array.FindIndex(data, value => !TextByte(value));
        byte[] trimmed = end < 0 ? data : data[..end];

        return trimmed.Length > 0 ? trimmed : null;
    }
}
