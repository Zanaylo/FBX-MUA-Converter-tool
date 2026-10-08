using System.Text;

namespace FbxToMua.Core.Sources;

public static class TextDisplay
{
    private const int ShiftJisCodePage = 932;

    private static readonly Encoding ShiftJis = CreateShiftJis();

    public static string Of(string latin1) => ShiftJis.GetString(Encoding.Latin1.GetBytes(latin1));

    public static string Of(byte[] bytes) => ShiftJis.GetString(bytes);

    public static byte[] ToShiftJis(string text) => ShiftJis.GetBytes(text);

    public static string Stored(string text) => Encoding.Latin1.GetString(ToShiftJis(text));

    private static Encoding CreateShiftJis()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return Encoding.GetEncoding(ShiftJisCodePage);
    }
}

public static class StageNames
{
    private const string Prefix = "bg_";
    private const int Longest = 40;

    private const string Unnamed = Prefix + "stage";

    public static string Stem(string name, string fallback = Unnamed)
    {
        StringBuilder stem = new(Prefix);
        bool gap = false;

        foreach (char letter in name)
        {
            if (letter < 0x80 && char.IsAsciiLetterOrDigit(letter))
            {
                if (gap && stem.Length > Prefix.Length)
                    stem.Append('_');

                stem.Append(char.ToLowerInvariant(letter));
                gap = false;
                continue;
            }

            gap = true;
        }

        string built = stem.Length == Prefix.Length ? fallback : stem.ToString();

        return built.Length > Longest ? built[..Longest] : built;
    }
}
