using FbxToMua.Core.Export;
using FbxToMua.Core.Install;
using FbxToMua.Core.Sources;
using FbxToMua.Core.Workflows;

namespace FbxToMua.App.ViewModels;

public sealed record GameChoice(string Title, string Folder)
{
    public override string ToString() => Title;

    public static List<GameChoice> Detected(IEnumerable<KnownGame> games, ISteamLibrary steam)
    {
        return games.Select(known => (known, folder: KnownInstalls.Find(steam, known)))
            .Where(found => found.folder is not null)
            .Select(found => new GameChoice(found.known.Title, found.folder!))
            .ToList();
    }
}

public sealed record StageChoice(string Folder, string Name)
{
    public string Shown => Name.Length == 0 || Name == Folder ? Folder : $"{Name}  ({Folder})";

    public override string ToString() => Shown;

    public static StageChoice Of(SourceStage stage) => new(stage.Folder, TextDisplay.Of(stage.Name));
}

public sealed record TargetChoice(StageTarget Target, string? ReplacedBy)
{
    public string Shown => ReplacedBy is null ? Target.Key : $"{Target.Key}  (now: {ReplacedBy})";

    public override string ToString() => Shown;
}

public sealed record ArcGameChoice(ArcGame Game, string Title)
{
    public static readonly IReadOnlyList<ArcGameChoice> All = [new(ArcGame.Bbcf, "BBCF"), new(ArcGame.Bbtag, "BBTAG")];

    public override string ToString() => Title;
}
