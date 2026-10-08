namespace FbxToMua.Core.Sources;

public enum GameKind
{
    None,
    Uni2,
    Uni,
    Mbtl,
    Mbaa,
    Dfci,
    Uniel,
    Bbtag,
    Bbcf,
    P4u2,
}

public static class GameFolder
{
    public static GameKind Detect(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return GameKind.None;

        bool Has(string name) => File.Exists(Path.Combine(folder, name));

        if (Has("MBAA.exe") && Has("0001.p"))
            return GameKind.Mbaa;

        if (Has("MBTL.exe"))
            return GameKind.Mbtl;

        if (Has("uni2.exe"))
            return GameKind.Uni2;

        if (Has("UNIst.exe") || Has("UNIclr.exe"))
            return GameKind.Uni;

        if (Has("UNIEL.exe"))
            return GameKind.Uniel;

        if (Has(Path.Combine("Bgm", "bgm.txt")) && (Has("RingGame.exe") || Has("eboot.bin")))
            return GameKind.Dfci;

        if (Has("BBTAG.exe"))
            return GameKind.Bbtag;

        if (Has("BBCF.exe"))
            return GameKind.Bbcf;

        return Has("P4U2.exe") ? GameKind.P4u2 : GameKind.None;
    }

    public static bool IsArcSys(GameKind game) => game is GameKind.Bbtag or GameKind.Bbcf or GameKind.P4u2;

    public static string Title(GameKind game)
    {
        return game switch
        {
            GameKind.Uni2 => "UNDER NIGHT IN-BIRTH II Sys:Celes",
            GameKind.Uni => "UNDER NIGHT IN-BIRTH Exe:Late[st]",
            GameKind.Mbtl => "MELTY BLOOD: TYPE LUMINA",
            GameKind.Mbaa => "MELTY BLOOD Actress Again Current Code",
            GameKind.Dfci => "DENGEKI BUNKO FIGHTING CLIMAX IGNITION",
            GameKind.Uniel => "UNDER NIGHT IN-BIRTH Exe:Late",
            GameKind.Bbtag => "BLAZBLUE CROSS TAG BATTLE",
            GameKind.Bbcf => "BLAZBLUE CENTRALFICTION",
            GameKind.P4u2 => "PERSONA 4 ARENA ULTIMAX",
            _ => "nothing this tool knows",
        };
    }
}
