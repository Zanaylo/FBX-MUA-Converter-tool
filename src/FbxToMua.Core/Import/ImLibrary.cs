using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Import;

public sealed class ImLibrary(string uni2Folder)
{
    private const int SlotFirst = 28;
    private const int IdLast = 998;
    private const int TrainingStage = 90;
    private const int DebugStage = 99;
    private const string LibraryKey = "Lib";

    public string Root => Path.Combine(uni2Folder, "UNI2-IM", "Mods", "bg");

    public bool Installed => Directory.Exists(Path.Combine(uni2Folder, "UNI2-IM"));

    public int FirstFree()
    {
        HashSet<int> taken = [.. Owned(), .. Registered()];

        for (int id = SlotFirst; id <= IdLast; ++id)
        {
            if (id == TrainingStage || id == DebugStage || taken.Contains(id))
                continue;

            if (!Directory.Exists(Path.Combine(Root, $"bg{id:D3}")))
                return id;
        }

        return -1;
    }

    public string? Install(ImStage stage)
    {
        int id = FirstFree();

        if (!Installed || id < 0)
            return null;

        string folder = $"bg{id:D3}";
        string target = Path.Combine(Root, folder);
        ImStageFolder.Write(stage, target, folder);

        return target;
    }

    private IEnumerable<int> Owned()
    {
        using Uni2Source? game = Uni2Source.Open(uni2Folder);

        return game is null ? [] : game.Stages().Select(stage => BgListText.NumberOf(stage.Folder)).Where(number => number >= 0).ToList();
    }

    private IEnumerable<int> Registered()
    {
        string ini = Path.Combine(uni2Folder, "UNI2-IM", "UNI2_IM.ini");

        if (!File.Exists(ini))
            return [];

        return File.ReadLines(ini)
            .Where(line => line.StartsWith(LibraryKey, StringComparison.Ordinal) && line.Contains('='))
            .Select(line => int.TryParse(line[LibraryKey.Length..line.IndexOf('=')], out int id) ? id : -1)
            .Where(id => id >= 0)
            .ToList();
    }
}
