using System.Text;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Import;

public sealed record ImStage(string Name, string Title, string From, string Source, GameKind Game, ArcStageResult Result);

public static class ImStageFolder
{
    public const string NoteFile = "stage.txt";
    private const string ObjectFile = "object.txt";
    private const int NameBytes = 62;

    public static string Tag(GameKind game)
    {
        return game switch
        {
            GameKind.Bbtag => " (BBTAG)",
            GameKind.Bbcf => " (BBCF)",
            GameKind.P4u2 => " (P4U2)",
            _ => string.Empty,
        };
    }

    public static string NoteOf(ImStage stage)
    {
        string block = ImStageBlock.For(stage.Source, stage.Game, stage.Result.Tilt, stage.Title);

        return ImStageNote.Build(SafeName(stage.Name), stage.From, stage.Source, stage.Result, block);
    }

    public static void Write(ImStage stage, string folder, string? renamedTo = null)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, StageFolder.ModelFile), stage.Result.Model);

        foreach ((string name, byte[] data) in stage.Result.Images)
            File.WriteAllBytes(Binary.DiskName.Path(folder, name), data);

        foreach ((string name, byte[] data) in stage.Result.Layer)
        {
            bool objects = string.Equals(name, ObjectFile, StringComparison.OrdinalIgnoreCase);
            File.WriteAllBytes(Binary.DiskName.Path(folder, name), objects && renamedTo is not null ? Renamed(data, stage.Source, renamedTo) : data);
        }

        File.WriteAllBytes(Path.Combine(folder, NoteFile), TextDisplay.ToShiftJis(NoteOf(stage)));
    }

    public static byte[] Renamed(byte[] objects, string stage, string folder)
    {
        string text = Encoding.Latin1.GetString(objects).Replace($"./bg/{stage}/", $"./bg/{folder}/", StringComparison.Ordinal);
        int table = text.IndexOf("<-", StringComparison.Ordinal);
        int open = table < 0 ? -1 : text.IndexOf('{', table);
        int end = open < 0 ? -1 : BgListText.MatchPair(text, open);

        return Encoding.Latin1.GetBytes(end > 0 && end < text.Length ? text[..end] : text);
    }

    private static string SafeName(string name)
    {
        string clean = name.Replace("\"", string.Empty, StringComparison.Ordinal);

        while (TextDisplay.ToShiftJis(clean).Length > NameBytes)
            clean = clean[..^1];

        return clean;
    }
}
