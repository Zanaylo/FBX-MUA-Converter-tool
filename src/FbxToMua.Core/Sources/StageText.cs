using System.Globalization;
using System.Text.RegularExpressions;
using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Sources;

public static partial class StageText
{
    private const int FogAlpha = 3;
    private const float FogAlphaFloor = 0.001f;
    private const bool BloomBakedUnlessAsked = false;

    [GeneratedRegex(@"^\s*\[\s*([-+0-9.eE]+)\s*,\s*([-+0-9.eE]+)\s*,\s*([-+0-9.eE]+)")]
    private static partial Regex TriplePattern();

    [GeneratedRegex(@"^\s*\[\s*([-+0-9.eE]+)\s*,\s*([-+0-9.eE]+)\s*,\s*([-+0-9.eE]+)\s*,\s*([-+0-9.eE]+)")]
    private static partial Regex QuadPattern();

    [GeneratedRegex(@"^\s*([-+0-9.eE]+)")]
    private static partial Regex SinglePattern();

    public static Framing Framing(string text) => FramingFrom(key => Field(text, key));

    public static Framing FramingFrom(Func<string, string> field)
    {
        Framing framing = Export.Framing.Neutral;

        if (TryTriple(field("Scale"), out Float3 scale))
            framing.Scale = scale;

        if (TryTriple(field("Position"), out Float3 position))
            framing.Position = position;

        if (TrySingle(field("ViewRotationX"), out float tilt))
            framing.Tilt = tilt;

        if (TrySingle(field("ViewRotationY"), out float turn))
            framing.Turn = turn;

        framing.Look = new StageLook(FogFrom(field), BloomFrom(field));

        return framing;
    }

    private static FogLook FogFrom(Func<string, string> field)
    {
        FogLook off = FogLook.Off;
        bool lit = TryQuad(field("FogColor"), out Float4 colour) && colour[FogAlpha] >= FogAlphaFloor;

        return new FogLook(
            lit && Single(field, "IsFog", 0.0f) != 0.0f,
            Single(field, "FogStart", off.Start),
            Single(field, "FogEnd", off.End),
            colour[0],
            colour[1],
            colour[2],
            TrySingle(field("VanishingPoint"), out _));
    }

    private static BloomLook BloomFrom(Func<string, string> field)
    {
        BloomLook off = BloomLook.Off;

        return new BloomLook(
            BloomBakedUnlessAsked,
            Single(field, "BGBloomBlightness", off.Brightness),
            Single(field, "BGBloomPower", off.Power),
            Single(field, "BGBloomBiassR", off.Red),
            Single(field, "BGBloomBiassG", off.Green),
            Single(field, "BGBloomBiassB", off.Blue),
            Single(field, "BGBloomBlurRadius", off.Radius),
            Single(field, "BGBloomAlpha", off.Strength));
    }

    private static float Single(Func<string, string> field, string key, float fallback) => TrySingle(field(key), out float value) ? value : fallback;

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

    public static bool TryQuad(string text, out Float4 value)
    {
        value = new Float4();
        Match match = QuadPattern().Match(text);

        if (!match.Success)
            return false;

        for (int k = 0; k < Float4.Size; ++k)
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
