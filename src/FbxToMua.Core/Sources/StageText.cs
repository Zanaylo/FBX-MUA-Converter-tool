using System.Globalization;
using System.Text.RegularExpressions;
using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Sources;

public static partial class StageText
{
    [GeneratedRegex(@"^\s*\[\s*([-+0-9.eE]+)\s*,\s*([-+0-9.eE]+)\s*,\s*([-+0-9.eE]+)")]
    private static partial Regex TriplePattern();

    [GeneratedRegex(@"^\s*([-+0-9.eE]+)")]
    private static partial Regex SinglePattern();

    public static Framing Framing(string text)
    {
        Framing framing = Export.Framing.Neutral;

        if (TryTriple(Field(text, "Scale"), out Float3 scale))
            framing.Scale = scale;

        if (TryTriple(Field(text, "Position"), out Float3 position))
            framing.Position = position;

        if (TrySingle(Field(text, "ViewRotationX"), out float tilt))
            framing.Tilt = tilt;

        if (TrySingle(Field(text, "ViewRotationY"), out float turn))
            framing.Turn = turn;

        return framing;
    }

    public static string Field(string text, string key)
    {
        string wanted = key + " =";
        int at = text.IndexOf(wanted, StringComparison.Ordinal);

        if (at < 0)
            return string.Empty;

        int equals = text.IndexOf('=', at);
        int stop = text.IndexOf('\n', at);

        if (equals < 0 || (stop >= 0 && equals > stop))
            return string.Empty;

        return stop < 0 ? text[(equals + 1)..] : text[(equals + 1)..stop];
    }

    public static bool TryTriple(string text, out Float3 value)
    {
        value = new Float3();
        Match match = TriplePattern().Match(text);

        if (!match.Success)
            return false;

        for (int k = 0; k < 3; ++k)
        {
            if (!float.TryParse(match.Groups[k + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return false;

            value[k] = parsed;
        }

        return true;
    }

    public static bool TrySingle(string text, out float value)
    {
        value = 0.0f;
        Match match = SinglePattern().Match(text);

        return match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
