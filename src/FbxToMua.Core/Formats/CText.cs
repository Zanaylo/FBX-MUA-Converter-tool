using System.Globalization;

namespace FbxToMua.Core.Formats;

public static class CText
{
    public static readonly char[] Space = [' ', '\t', '\r', '\n', '\v', '\f'];

    public static int Atoi(string text)
    {
        int at = SkipSpace(text, 0);
        int start = at;

        if (at < text.Length && (text[at] == '-' || text[at] == '+'))
            ++at;

        while (at < text.Length && char.IsAsciiDigit(text[at]))
            ++at;

        return long.TryParse(text.AsSpan(start, at - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)
            ? (int)Math.Clamp(value, int.MinValue, int.MaxValue)
            : 0;
    }

    public static double Atof(string text)
    {
        int at = SkipSpace(text, 0);
        int start = at;

        if (at < text.Length && (text[at] == '-' || text[at] == '+'))
            ++at;

        while (at < text.Length && char.IsAsciiDigit(text[at]))
            ++at;

        if (at < text.Length && text[at] == '.')
        {
            ++at;

            while (at < text.Length && char.IsAsciiDigit(text[at]))
                ++at;
        }

        if (at < text.Length && (text[at] == 'e' || text[at] == 'E'))
        {
            int exponent = at + 1;

            if (exponent < text.Length && (text[exponent] == '-' || text[exponent] == '+'))
                ++exponent;

            if (exponent < text.Length && char.IsAsciiDigit(text[exponent]))
            {
                at = exponent;

                while (at < text.Length && char.IsAsciiDigit(text[at]))
                    ++at;
            }
        }

        return double.TryParse(text.AsSpan(start, at - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : 0.0;
    }

    public static bool TryFloats(string text, int count, out float[] values)
    {
        values = new float[count];
        string inner = text.Trim(Space).TrimStart('[').Split(']')[0];
        string[] parts = inner.Split(',');

        if (parts.Length < count)
            return false;

        for (int i = 0; i < count; ++i)
        {
            if (!float.TryParse(parts[i].Trim(Space), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                return false;
        }

        return true;
    }

    private static int SkipSpace(string text, int at)
    {
        while (at < text.Length && Space.Contains(text[at]))
            ++at;

        return at;
    }
}
