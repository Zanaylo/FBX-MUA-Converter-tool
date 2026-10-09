using FbxToMua.Core.Export;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Install;

public sealed record InstallReport(bool Done, string Message);

public sealed class StageInstaller(IInstallRecords records, ISteamLibrary steam)
{
    private const string StagePrefix = "bg_";

    public string? GameFolder(ArcGame game)
    {
        string? chosen = records.GameFolder(game);

        if (chosen is not null && Matches(game, chosen))
            return chosen;

        IReadOnlyList<string> installs = Workflows.KnownInstalls.SteamFoldersOf(KindOf(game));

        return installs.Select(steam.GameFolder).FirstOrDefault(folder => folder is not null && Matches(game, folder));
    }

    public static GameKind KindOf(ArcGame game) => game == ArcGame.Bbcf ? GameKind.Bbcf : GameKind.Bbtag;

    public bool ChooseGameFolder(ArcGame game, string folder)
    {
        if (!Matches(game, folder))
            return false;

        records.ChooseGameFolder(game, folder);
        return true;
    }

    public IReadOnlyList<StageTarget> Targets(ArcGame game) => Store(game)?.Targets() ?? [];

    public string? Installed(ArcGame game, StageTarget target) => records.Installed(game, target);

    public bool HasBackup(ArcGame game, StageTarget target)
    {
        string folder = records.BackupFolder(game, target);

        return ArchiveParts.All.All(part => File.Exists(BackupPath(folder, part)));
    }

    public string ModelOf(ArcGame game, StageTarget target)
    {
        string backup = BackupPath(records.BackupFolder(game, target), ArchivePart.Scene);
        IStageStore? store = Store(game);

        if (store is not null && File.Exists(backup))
            return InstallRules.ModelName(store.Plain(target, ArchivePart.Scene, File.ReadAllBytes(backup)));

        byte[]? scene = store?.ReadPlain(target, ArchivePart.Scene);

        if (scene is not null && InstallRules.Loadable(scene))
            return InstallRules.ModelName(scene);

        return target.Stem.StartsWith(StagePrefix, StringComparison.Ordinal) ? target.Stem[StagePrefix.Length..] : target.Stem;
    }

    public InstallReport Install(ArcGame game, StageTarget target, ExportResult result, string stageName)
    {
        IStageStore? store = Store(game);

        if (store is null)
            return new InstallReport(false, $"No {Title(game)} folder was found.");

        string note = BackUp(game, target, store);
        ExportArchives archives = StagePackager.Package(result, game, ModelOf(game, target));

        try
        {
            foreach (ArchivePart part in ArchiveParts.All)
                store.WritePlain(target, part, ArchiveParts.Of(archives, part));
        }
        catch (IOException)
        {
            return new InstallReport(false, $"Could not write {target.Stem}. Close the game and try again.");
        }
        catch (UnauthorizedAccessException)
        {
            return new InstallReport(false, $"Could not write {target.Stem}. Close the game and try again.");
        }

        records.Record(game, target, stageName);

        return new InstallReport(true, $"Installed over {target.Stem} in {Title(game)}.{note}");
    }

    public InstallReport Restore(ArcGame game, StageTarget target)
    {
        IStageStore? store = Store(game);

        if (store is null || !HasBackup(game, target))
            return new InstallReport(false, $"There is no backup of {target.Stem}.");

        string folder = records.BackupFolder(game, target);

        foreach (ArchivePart part in ArchiveParts.All)
            store.WriteRaw(target, part, File.ReadAllBytes(BackupPath(folder, part)));

        foreach (ArchivePart part in ArchiveParts.All)
            File.Delete(BackupPath(folder, part));

        records.Record(game, target, null);

        return new InstallReport(true, $"{target.Stem} is the original again.");
    }

    private static string Title(ArcGame game) => game == ArcGame.Bbcf ? "BBCF" : "BBTAG";

    private static bool Matches(ArcGame game, string folder)
    {
        return Sources.GameFolder.Detect(folder) == KindOf(game);
    }

    private static string BackupPath(string folder, ArchivePart part) => Path.Combine(folder, "stage" + ArchiveParts.Suffix(part));

    private IStageStore? Store(ArcGame game)
    {
        string? folder = GameFolder(game);

        return folder is null ? null : StageStores.For(game, folder);
    }

    private string BackUp(ArcGame game, StageTarget target, IStageStore store)
    {
        if (HasBackup(game, target))
            return string.Empty;

        if (records.Installed(game, target) is not null)
            return $" {target.Stem} was already replaced, so it has no backup.";

        byte[]? scene = store.ReadPlain(target, ArchivePart.Scene);

        if (scene is null || !InstallRules.Loadable(scene))
            return $" {target.Stem} was not a working stage, so no backup was made. Verify the game files in Steam to get it back.";

        string folder = records.BackupFolder(game, target);
        Directory.CreateDirectory(folder);

        foreach (ArchivePart part in ArchiveParts.All)
            File.WriteAllBytes(BackupPath(folder, part), store.ReadRaw(target, part) ?? []);

        return string.Empty;
    }
}
