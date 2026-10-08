using FbxToMua.Core.Sources.Mbaa;

namespace FbxToMua.Core.Sources;

public sealed class MbaaSource : IStageSource
{
    public const string CardFile = "Thumbnail.dds";
    private const string StageFolderName = "bg";
    private const string StageSuffix = ".dat";

    private readonly MbaaArchive _archive;
    private readonly SortedDictionary<string, uint> _stages = new(StringComparer.Ordinal);
    private string _ready = string.Empty;
    private MbaaResult? _result;

    public GameKind Game => GameKind.Mbaa;

    private MbaaSource(MbaaArchive archive)
    {
        _archive = archive;

        foreach (PackedEntry entry in archive.List(StageFolderName))
        {
            if (entry.Name.EndsWith(StageSuffix, StringComparison.OrdinalIgnoreCase))
                _stages[entry.Name[..^StageSuffix.Length].ToLowerInvariant()] = entry.Size;
        }
    }

    public static MbaaSource? Open(string folder)
    {
        MbaaArchive? archive = MbaaArchive.Open(folder);

        if (archive is null)
            return null;

        MbaaSource source = new(archive);

        return source._stages.Count > 0 ? source : null;
    }

    public MbaaResult? Converted(string stage) => Built(stage) ? _result : null;

    public IReadOnlyList<SourceStage> Stages() => _stages.Select(stage => new SourceStage(stage.Key, stage.Key, stage.Value)).ToList();

    public IReadOnlyList<string> Files(string stage)
    {
        if (!Built(stage))
            return [];

        return [StageFolder.ModelFile, CardFile, .. _result!.Textures.Select(texture => texture.Name)];
    }

    public byte[]? Read(string stage, string file)
    {
        if (!Built(stage))
            return null;

        if (string.Equals(file, StageFolder.ModelFile, StringComparison.OrdinalIgnoreCase))
            return _result!.Model;

        if (string.Equals(file, CardFile, StringComparison.OrdinalIgnoreCase))
            return _result!.Card;

        return _result!.Textures.FirstOrDefault(texture => string.Equals(texture.Name, file, StringComparison.OrdinalIgnoreCase))?.Dds;
    }

    public string BgList()
    {
        System.Text.StringBuilder list = new();
        int index = 0;

        foreach (string stage in _stages.Keys)
            list.Append($"\tBg_{index++:D3} =\r\n\t{{\r\n\t\tName = \"{stage}\",\r\n\t\tDataFile = \"{stage}\",\r\n\r\n{MbaaStage.Block()}\t}}\r\n");

        return list.ToString();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private bool Built(string stage)
    {
        string key = stage.ToLowerInvariant();

        if (_ready == key)
            return _result is not null;

        _ready = key;
        _result = null;
        byte[]? dat = _stages.ContainsKey(key) ? _archive.Read(StageFolderName, stage + StageSuffix) : null;
        _result = dat is null ? null : MbaaStage.Convert(dat);

        return _result is not null;
    }
}
