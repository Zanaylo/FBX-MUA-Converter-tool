using System.Text.Json;
using FbxToMua.Core.Export;

namespace FbxToMua.Core.Install;

public interface IInstallRecords
{
    string? GameFolder(ArcGame game);

    void ChooseGameFolder(ArcGame game, string folder);

    string? Installed(ArcGame game, StageTarget target);

    void Record(ArcGame game, StageTarget target, string? stageName);

    string BackupFolder(ArcGame game, StageTarget target);
}

public sealed class InstallRecords : IInstallRecords
{
    private const string RecordFile = "installs.json";
    private const string BackupRoot = "Backup";

    private sealed class Records
    {
        public Dictionary<string, string> Folders { get; set; } = [];
        public Dictionary<string, Dictionary<string, string>> Installs { get; set; } = [];
    }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _root;
    private readonly Records _records;

    public InstallRecords(string root)
    {
        _root = root;
        string path = Path.Combine(root, RecordFile);
        _records = File.Exists(path) ? JsonSerializer.Deserialize<Records>(File.ReadAllText(path)) ?? new Records() : new Records();
    }

    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FbxToMua");

    public string? GameFolder(ArcGame game) => _records.Folders.GetValueOrDefault(game.ToString());

    public void ChooseGameFolder(ArcGame game, string folder)
    {
        _records.Folders[game.ToString()] = folder;
        Save();
    }

    public string? Installed(ArcGame game, StageTarget target)
    {
        return _records.Installs.GetValueOrDefault(game.ToString())?.GetValueOrDefault(target.Key);
    }

    public void Record(ArcGame game, StageTarget target, string? stageName)
    {
        if (!_records.Installs.TryGetValue(game.ToString(), out Dictionary<string, string>? installs))
            _records.Installs[game.ToString()] = installs = [];

        if (stageName is null)
            installs.Remove(target.Key);
        else
            installs[target.Key] = stageName;

        Save();
    }

    public string BackupFolder(ArcGame game, StageTarget target) => Path.Combine(_root, BackupRoot, game.ToString().ToUpperInvariant(), target.Group, target.Stem);

    private void Save()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, RecordFile), JsonSerializer.Serialize(_records, Options));
    }
}
