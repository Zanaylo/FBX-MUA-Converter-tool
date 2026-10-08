using FbxToMua.Core.Export;
using FbxToMua.Core.Install;

namespace FbxToMua.Core.Tests.Fakes;

public sealed class MemoryInstallRecords(string backupRoot) : IInstallRecords
{
    private readonly Dictionary<ArcGame, string> _folders = [];
    private readonly Dictionary<(ArcGame, string), string> _installs = [];

    public string? GameFolder(ArcGame game) => _folders.GetValueOrDefault(game);

    public void ChooseGameFolder(ArcGame game, string folder) => _folders[game] = folder;

    public string? Installed(ArcGame game, StageTarget target) => _installs.GetValueOrDefault((game, target.Key));

    public void Record(ArcGame game, StageTarget target, string? stageName)
    {
        if (stageName is null)
            _installs.Remove((game, target.Key));
        else
            _installs[(game, target.Key)] = stageName;
    }

    public string BackupFolder(ArcGame game, StageTarget target) => Path.Combine(backupRoot, game.ToString(), target.Group, target.Stem);
}
