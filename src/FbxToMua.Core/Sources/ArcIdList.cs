using System.Text;

namespace FbxToMua.Core.Sources;

public static class ArcIdList
{
    public const string English = "data/localize/eng_idlist.txt";
    public const string Japanese = "data/localize/jpn_idlist.txt";

    private const string StageId = "BG_";
    private const string StagePrefix = "bg_";
    private const char ByteOrderMark = '﻿';

    private static readonly (char Code, char Shown)[] Glyphs = [('|', '\''), ('^', ':')];

    public static string Text(byte[]? data) => data is null ? string.Empty : Encoding.Unicode.GetString(data).TrimStart(ByteOrderMark);

    public static Dictionary<string, string> StageNames(string english, string japanese)
    {
        Dictionary<string, string> spaced = Entries(japanese);
        Dictionary<string, string> names = new(StringComparer.Ordinal);

        foreach ((string stage, string name) in Entries(english))
            names[stage] = Spaced(name, spaced.GetValueOrDefault(stage));

        return names;
    }

    private static Dictionary<string, string> Entries(string text)
    {
        string[] lines = text.Split('\n');
        Dictionary<string, string> entries = new(StringComparer.Ordinal);

        for (int i = 0; i + 1 < lines.Length; ++i)
        {
            string id = lines[i].Trim();

            if (!id.StartsWith(StageId, StringComparison.Ordinal))
                continue;

            string stem = id[StageId.Length..];
            string name = Shown(lines[++i].Trim());

            if (name.Length > 0 && name != stem)
                entries[StagePrefix + stem.ToLowerInvariant()] = name;
        }

        return entries;
    }

    private static string Spaced(string name, string? japanese) => japanese is not null && Squeezed(japanese) == Squeezed(name) ? japanese : name;

    private static string Squeezed(string text) => text.Replace(" ", string.Empty, StringComparison.Ordinal);

    private static string Shown(string text) => Glyphs.Aggregate(text, (shown, glyph) => shown.Replace(glyph.Code, glyph.Shown));
}
