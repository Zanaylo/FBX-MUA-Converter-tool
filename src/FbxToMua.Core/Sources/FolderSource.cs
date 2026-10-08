using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.FbxText;

namespace FbxToMua.Core.Sources;

public abstract class FolderSource(string folder) : IStageSource
{
    protected string Bg { get; } = Path.Combine(folder, "bg");

    public abstract GameKind Game { get; }

    public bool IsOpen => File.Exists(Path.Combine(Bg, StageTally.BgListFile));

    public IReadOnlyList<SourceStage> Stages()
    {
        if (!Directory.Exists(Bg))
            return [];

        string bgList = BgList();
        List<SourceStage> stages = [];

        foreach (string directory in Directory.EnumerateDirectories(Bg, "bg*").Order(StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(directory);
            string? model = Model(name);

            if (model is null)
                continue;

            string title = Named(BgListText.NumberOf(name));

            if (title.Length == 0)
            {
                string? block = BgListText.Block(bgList, name);
                string? field = block is null ? null : BgListText.Field(block, "Name");
                title = field is null ? string.Empty : BgListText.Unquoted(field);
            }

            stages.Add(new SourceStage(name, title, new FileInfo(model).Length));
        }

        return stages;
    }

    public IReadOnlyList<string> Files(string stage)
    {
        string directory = Path.Combine(Bg, stage);

        if (!Directory.Exists(directory))
            return [];

        return Directory.EnumerateFiles(directory).Select(path => Exported(DiskName.FromDisk(Path.GetFileName(path)))).Where(name => name.Length > 0).ToList();
    }

    public byte[]? Read(string stage, string file)
    {
        if (!string.Equals(file, StageFolder.ModelFile, StringComparison.OrdinalIgnoreCase))
            return Whole(DiskName.Path(Path.Combine(Bg, stage), file));

        string? model = Model(stage);
        byte[]? data = model is null ? null : Whole(model);

        return data is null ? null : Convert(data);
    }

    public string BgList()
    {
        byte[]? data = Whole(Path.Combine(Bg, StageTally.BgListFile));

        return data is null ? string.Empty : LittleEndian.Latin1(data);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    protected abstract string? Model(string stage);

    protected virtual string Exported(string name) => name;

    protected virtual byte[]? Convert(byte[] data) => data;

    protected virtual void Decode(byte[] data)
    {
    }

    protected virtual string Named(int number) => string.Empty;

    protected byte[]? Whole(string path)
    {
        if (!File.Exists(path))
            return null;

        byte[] data = File.ReadAllBytes(path);
        Decode(data);

        return data;
    }
}

public sealed class DfciSource(string folder) : FolderSource(folder)
{
    private Dictionary<int, string>? _english;

    public override GameKind Game => GameKind.Dfci;

    protected override string? Model(string stage)
    {
        return new[] { "bg.fbx", "bg.FBX" }.Select(name => Path.Combine(Bg, stage, name)).FirstOrDefault(File.Exists);
    }

    protected override string Exported(string name) => string.Equals(name, "bg.fbx", StringComparison.OrdinalIgnoreCase) ? StageFolder.ModelFile : name;

    protected override byte[]? Convert(byte[] data) => FbxTextToFbxEx.Convert(data);

    protected override string Named(int number)
    {
        _english ??= ReadEnglish();

        return _english.GetValueOrDefault(number, string.Empty);
    }

    private Dictionary<int, string> ReadEnglish()
    {
        Dictionary<int, string> english = [];
        byte[]? data = Whole(Path.Combine(Bg, "BgList_str.txt"));

        if (data is null)
            return english;

        foreach (string line in LittleEndian.Latin1(data).Split('\n'))
        {
            int equals = line.IndexOf('=');

            if (equals < 0)
                continue;

            string number = line[..equals].Trim(CText.Space);
            string value = line[(equals + 1)..].Trim(CText.Space);
            int digits = number.TakeWhile(char.IsAsciiDigit).Count();

            if (digits == 0 || value.Length == 0)
                continue;

            english[int.Parse(number[..digits], System.Globalization.CultureInfo.InvariantCulture)] = value;
        }

        return english;
    }
}

public sealed class UnielSource(string folder) : FolderSource(folder)
{
    public override GameKind Game => GameKind.Uniel;

    protected override string? Model(string stage)
    {
        string path = Path.Combine(Bg, stage, StageFolder.ModelFile);

        return File.Exists(path) ? path : null;
    }

    protected override string Exported(string name) => string.Equals(name, "bg.fbx", StringComparison.OrdinalIgnoreCase) ? string.Empty : name;

    protected override byte[]? Convert(byte[] data)
    {
        FbxExLocal.Apply(data);

        return data;
    }

    protected override void Decode(byte[] data) => UnielCipher.Decrypt(data);
}
