using System.Globalization;
using System.Text;
using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Import;

public static class ImStageNote
{
    public const string Header = "// UNI2 Improvement Mod\r\n";

    private static readonly string[] IdentityKeys = ["Name", "From", "Source"];

    public static string Build(string name, string from, string source, ArcStageResult result, string block)
    {
        StringBuilder note = new(Header);
        note.Append($"Name = \"{name}\"\r\nFrom = \"{from}\"\r\nSource = \"{source}\"\r\n");

        if (result.Flow.Count > 0)
            note.Append("Flow = [ ").Append(string.Join(", ", result.Flow.Select(rate => ((double)rate).ToString("F8", CultureInfo.InvariantCulture)))).Append(" ]\r\n");

        for (int i = 0; i < result.Lamps.Count; ++i)
            note.Append(LampLines(i, result.Lamps[i]));

        note.Append(FlipLines(result.Flips));
        note.Append(IntsLine("Once", result.Once));
        note.Append(IntsLine("Kick", result.Kick));

        if (result.Fading)
            note.Append("VertexAlpha = 1\r\n");

        return note.Append("\r\n").Append(Body(block)).ToString();
    }

    public static string Body(string note)
    {
        StringBuilder body = new();

        for (int at = 0; at < note.Length;)
        {
            int newline = note.IndexOf('\n', at);
            int end = newline < 0 ? note.Length : newline + 1;
            string line = note[at..end];
            at = end;

            if (IsIdentityLine(line) || (body.Length == 0 && WithoutLineEnd(line).Length == 0))
                continue;

            body.Append(line);
        }

        return body.ToString();
    }

    public static string FlipLines(IReadOnlyList<EvbFlip> flips)
    {
        StringBuilder lines = new();

        for (int i = 0; i < flips.Count; ++i)
        {
            EvbFlip flip = flips[i];

            if (flip.Rects.Count == 0 || flip.Frame.Count == 0)
                continue;

            lines.Append(CultureInfo.InvariantCulture, $"Flip{i}Rects = [ ");
            lines.Append(string.Join(", ", flip.Rects.Select(rect => ((double)rect).ToString("F6", CultureInfo.InvariantCulture))));
            lines.Append(CultureInfo.InvariantCulture, $" ]\r\nFlip{i} = [ ");

            List<string> runs = [];

            for (int at = 0; at < flip.Frame.Count;)
            {
                int run = 1;

                while (at + run < flip.Frame.Count && flip.Frame[at + run] == flip.Frame[at])
                    ++run;

                runs.Add($"{flip.Frame[at]}, {run}");
                at += run;
            }

            lines.Append(string.Join(", ", runs)).Append(" ]\r\n");
        }

        return lines.ToString();
    }

    private static string LampLines(int index, EvbLamp lamp)
    {
        if (lamp.Loop < 1 || lamp.Ramp.Count == 0)
            return string.Empty;

        string line = $"Lamp{index} = [ {lamp.Loop}" + string.Concat(lamp.Ramp.Select(ramp => $", {ramp.At}, {ramp.Target}, {ramp.Frames}")) + " ]\r\n";

        return lamp.From < 1 ? line : line + $"Lamp{index}From = {lamp.From}\r\n";
    }

    private static string IntsLine(string name, List<int> values)
    {
        return values.Count == 0 ? string.Empty : $"{name} = [ {string.Join(", ", values)} ]\r\n";
    }

    private static string WithoutLineEnd(string line) => line.TrimEnd('\r', '\n');

    private static string LeadingKey(string line)
    {
        int end = 0;

        while (end < line.Length && (char.IsAsciiLetterOrDigit(line[end]) || line[end] == '_'))
            ++end;

        int equals = end;

        while (equals < line.Length && line[equals] is ' ' or '\t')
            ++equals;

        return end == 0 || equals >= line.Length || line[equals] != '=' ? string.Empty : line[..end];
    }

    private static bool IsIdentityLine(string line)
    {
        return WithoutLineEnd(line) == WithoutLineEnd(Header) || IdentityKeys.Contains(LeadingKey(line));
    }
}

public static class ImStageBlock
{
    public static string For(string stage, GameKind game, float tilt, string readable)
    {
        string list = $"\tBg_000 =\r\n\t{{\r\n\t\tName = \"{readable}\",\r\n\t\tDataFile = \"{stage}\",\r\n\r\n{ArcLooks.Block(stage, game, tilt)}\t}}\r\n";

        return BgListText.Block(list, stage) ?? string.Empty;
    }
}
