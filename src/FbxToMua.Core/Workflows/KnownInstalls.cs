using FbxToMua.Core.Install;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Workflows;

public sealed record KnownGame(GameKind Game, string Title, IReadOnlyList<string> SteamFolders);

public static class KnownInstalls
{
    public static readonly IReadOnlyList<KnownGame> FrenchBread =
    [
        new(GameKind.Uni2, "UNDER NIGHT IN-BIRTH II Sys:Celes", ["UNDER NIGHT IN-BIRTH II Sys Celes"]),
        new(GameKind.Uni, "UNDER NIGHT IN-BIRTH Exe:Late[st]", ["UNDER NIGHT In-Birth Exe Late[st]"]),
        new(GameKind.Uniel, "UNDER NIGHT IN-BIRTH Exe:Late", ["UNDER NIGHT IN-BIRTH Exe Late"]),
        new(GameKind.Mbtl, "MELTY BLOOD: TYPE LUMINA", ["MELTY BLOOD TYPE LUMINA"]),
        new(GameKind.Mbaa, "MELTY BLOOD Actress Again Current Code", ["MELTY BLOOD Actress Again Current Code"]),
        new(GameKind.Dfci, "DENGEKI BUNKO FIGHTING CLIMAX IGNITION", ["DFCI - Arcade", "DENGEKI BUNKO FIGHTING CLIMAX IGNITION"]),
    ];

    public static readonly IReadOnlyList<KnownGame> ArcSystem =
    [
        new(GameKind.Bbtag, "BLAZBLUE CROSS TAG BATTLE", ["BlazBlue Cross Tag Battle", "BBTAG"]),
        new(GameKind.Bbcf, "BLAZBLUE CENTRALFICTION", ["BlazBlue Centralfiction"]),
        new(GameKind.P4u2, "PERSONA 4 ARENA ULTIMAX", ["P4U2", "Persona 4 Arena Ultimax"]),
    ];

    public static string? Find(ISteamLibrary steam, KnownGame known)
    {
        return known.SteamFolders.Select(steam.GameFolder).FirstOrDefault(folder => folder is not null && GameFolder.Detect(folder) == known.Game);
    }

    public static IReadOnlyList<string> SteamFoldersOf(GameKind game)
    {
        return FrenchBread.Concat(ArcSystem).FirstOrDefault(known => known.Game == game)?.SteamFolders ?? [];
    }
}
