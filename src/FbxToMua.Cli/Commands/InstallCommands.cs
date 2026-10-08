using FbxToMua.Core.Export;
using FbxToMua.Core.Install;
using FbxToMua.Core.Sources;
using FbxToMua.Core.Workflows;

namespace FbxToMua.Cli.Commands;

internal static class ArcGames
{
    public static ArcGame? Parse(string? name)
    {
        return name?.ToLowerInvariant() switch
        {
            "bbcf" => ArcGame.Bbcf,
            "bbtag" => ArcGame.Bbtag,
            _ => null,
        };
    }

    public static StageInstaller Installer() => new(new InstallRecords(InstallRecords.DefaultRoot), new SteamLibrary());

    public static StageTarget? Target(string? key)
    {
        int slash = key?.IndexOf('/') ?? -1;

        return slash <= 0 ? null : new StageTarget(key![..slash], key[(slash + 1)..]);
    }
}

public sealed class TargetsCommand : ICommand
{
    public string Name => "targets";

    public string Usage => "targets <bbcf|bbtag>";

    public int Run(Arguments arguments)
    {
        ArcGame? game = ArcGames.Parse(arguments.At(0));

        if (game is null)
            return Report.Usage(this);

        StageInstaller installer = ArcGames.Installer();
        string? folder = installer.GameFolder(game.Value);

        if (folder is null)
            return Report.Failure($"No {game} install found. Choose it with: FbxToMua game-folder {game.Value.ToString().ToLowerInvariant()} <folder>");

        Console.WriteLine($"{folder}:");

        foreach (StageTarget target in installer.Targets(game.Value))
        {
            string? installed = installer.Installed(game.Value, target);
            Console.WriteLine($"  {target.Key,-36} {(installed is null ? string.Empty : "replaced by " + installed)}");
        }

        return Report.Ok;
    }
}

public sealed class GameFolderCommand : ICommand
{
    public string Name => "game-folder";

    public string Usage => "game-folder <bbcf|bbtag> <folder>";

    public int Run(Arguments arguments)
    {
        ArcGame? game = ArcGames.Parse(arguments.At(0));
        string? folder = arguments.At(1);

        if (game is null || folder is null)
            return Report.Usage(this);

        return ArcGames.Installer().ChooseGameFolder(game.Value, folder)
            ? Report.Success($"{game} folder set to {folder}.")
            : Report.Failure($"{folder} is not a {game} install.");
    }
}

public sealed class InstallCommand : ICommand
{
    public string Name => "install";

    public string Usage => "install <game folder | stage folder> [stage] --to <bbcf|bbtag> --replace <group/bg_stem> [--name text]";

    public int Run(Arguments arguments)
    {
        ArcGame? game = ArcGames.Parse(arguments.Option("to"));
        StageTarget? target = ArcGames.Target(arguments.Option("replace"));
        string? folder = arguments.At(0);

        if (game is null || target is null || folder is null)
            return Report.Usage(this);

        ExportedStage exported = StageWorkflows.Export(folder, arguments.At(1), arguments.Option("name"));
        InstallReport report = ArcGames.Installer().Install(game.Value, target, exported.Result, exported.Name);

        return report.Done ? Report.Success(ExportSummary.Of(exported.Result) + " " + report.Message) : Report.Failure(report.Message);
    }
}

public sealed class RestoreCommand : ICommand
{
    public string Name => "restore";

    public string Usage => "restore <bbcf|bbtag> <group/bg_stem>";

    public int Run(Arguments arguments)
    {
        ArcGame? game = ArcGames.Parse(arguments.At(0));
        StageTarget? target = ArcGames.Target(arguments.At(1));

        if (game is null || target is null)
            return Report.Usage(this);

        InstallReport report = ArcGames.Installer().Restore(game.Value, target);

        return report.Done ? Report.Success(report.Message) : Report.Failure(report.Message);
    }
}
