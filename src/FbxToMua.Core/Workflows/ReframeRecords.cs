using System.Text.Json;
using FbxToMua.Core.Export;

namespace FbxToMua.Core.Workflows;

public interface IReframeRecords
{
    Reframe Of(string folder, string? stage);

    void Keep(string folder, string? stage, Reframe reframe);
}

public sealed class ReframeRecords : IReframeRecords
{
    private const string RecordFile = "reframes.json";
    private const char KeySeparator = '|';

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _root;
    private readonly Dictionary<string, Reframe> _reframes;

    public ReframeRecords(string root)
    {
        _root = root;
        string path = Path.Combine(root, RecordFile);
        Dictionary<string, Reframe>? read = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, Reframe>>(File.ReadAllText(path)) : null;
        _reframes = new Dictionary<string, Reframe>(read ?? [], StringComparer.OrdinalIgnoreCase);
    }

    public Reframe Of(string folder, string? stage) => _reframes.GetValueOrDefault(KeyOf(folder, stage), Reframe.None);

    public void Keep(string folder, string? stage, Reframe reframe)
    {
        string key = KeyOf(folder, stage);

        if (reframe.IsNone)
            _reframes.Remove(key);
        else
            _reframes[key] = reframe;

        Save();
    }

    private static string KeyOf(string folder, string? stage) => Path.TrimEndingDirectorySeparator(folder) + KeySeparator + (stage ?? string.Empty);

    private void Save()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, RecordFile), JsonSerializer.Serialize(_reframes, Options));
    }
}
