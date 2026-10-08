using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Formats.Objects;

public readonly record struct ObjectFrame(string Name, int Wait);

public sealed class ObjectEntry
{
    public int Number { get; set; }
    public List<ObjectFrame> Frames { get; } = [];
    public int Prio { get; set; }
    public Float3 Start;
    public int Delay { get; set; }
}

public static class ObjectList
{
    public const int MostEntries = 99;
    private const string EntryKey = "data";
    private const int NumberDigits = 3;

    public static List<ObjectEntry> Read(string text)
    {
        string plain = Uncommented(text);
        List<ObjectEntry> entries = [];

        for (int at = plain.IndexOf(EntryKey, StringComparison.Ordinal); at >= 0;
            at = plain.IndexOf(EntryKey, at + 1, StringComparison.Ordinal))
        {
            ObjectEntry? entry = ReadEntry(plain, ref at);

            if (entry is not null)
                entries.Add(entry);
        }

        return entries.OrderBy(entry => entry.Number).Take(MostEntries).ToList();
    }

    public static string SpriteFile(string objectList)
    {
        int key = objectList.IndexOf("panidata", StringComparison.Ordinal);
        int open = key < 0 ? -1 : objectList.IndexOf('"', key);
        int close = open < 0 ? -1 : objectList.IndexOf('"', open + 1);

        if (close < 0)
            return string.Empty;

        string path = objectList[(open + 1)..close];
        int slash = path.LastIndexOfAny(['/', '\\']);

        return slash < 0 ? path : path[(slash + 1)..];
    }

    private static string Uncommented(string text)
    {
        System.Text.StringBuilder plain = new(text.Length);

        for (int at = 0; at < text.Length; ++at)
        {
            if (string.CompareOrdinal(text, at, "//", 0, 2) == 0)
            {
                at = text.IndexOf('\n', at);

                if (at < 0)
                    break;

                plain.Append('\n');
                continue;
            }

            if (string.CompareOrdinal(text, at, "/*", 0, 2) == 0)
            {
                at = text.IndexOf("*/", at + 2, StringComparison.Ordinal);

                if (at < 0)
                    break;

                ++at;
                continue;
            }

            plain.Append(text[at]);
        }

        return plain.ToString();
    }

    private static bool Number(string text, int at, out int number)
    {
        number = 0;

        if (at + NumberDigits > text.Length)
            return false;

        for (int i = at; i < at + NumberDigits; ++i)
        {
            if (text[i] < '0' || text[i] > '9')
                return false;
        }

        number = int.Parse(text.AsSpan(at, NumberDigits), System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    private static string Field(string record, string key)
    {
        string wanted = key + "=";
        string compact = string.Concat(record.Where(letter => letter is not (' ' or '\t' or '\r' or '\n')));

        for (int at = compact.IndexOf(wanted, StringComparison.Ordinal); at >= 0;
            at = compact.IndexOf(wanted, at + 1, StringComparison.Ordinal))
        {
            bool starts = at == 0 || compact[at - 1] == '{' || compact[at - 1] == ',';

            if (!starts)
                continue;

            int value = at + wanted.Length;

            if (value < compact.Length && compact[value] == '"')
            {
                int close = compact.IndexOf('"', value + 1);
                return close < 0 ? string.Empty : compact[(value + 1)..close];
            }

            int stop = compact.IndexOfAny([',', '}'], value);
            return stop < 0 ? compact[value..] : compact[value..stop];
        }

        return string.Empty;
    }

    private static void Apply(string record, ObjectEntry entry)
    {
        string tag = Field(record, "tag");

        switch (tag)
        {
            case "frm":
                entry.Frames.Add(new ObjectFrame(Field(record, "name"), CText.Atoi(Field(record, "wait"))));
                return;
            case "prio":
                entry.Prio = CText.Atoi(Field(record, "val"));
                return;
            case "startdelay":
                entry.Delay = CText.Atoi(Field(record, "val"));
                return;
            case "startpos":
                entry.Start = new Float3((float)CText.Atof(Field(record, "x")), (float)CText.Atof(Field(record, "y")),
                    (float)CText.Atof(Field(record, "z")));
                return;
        }
    }

    private static ObjectEntry? ReadEntry(string text, ref int at)
    {
        if (!Number(text, at + EntryKey.Length, out int number))
            return null;

        int equals = SkipBlank(text, at + EntryKey.Length + NumberDigits);

        if (equals < 0 || text[equals] != '=')
            return null;

        int open = SkipBlank(text, equals + 1);

        if (open < 0 || text[open] != '[')
            return null;

        int close = text.IndexOf(']', open);

        if (close < 0)
            return null;

        ObjectEntry entry = new() { Number = number };

        for (int record = text.IndexOf('{', open); record >= 0 && record < close; record = text.IndexOf('{', record + 1))
        {
            int end = text.IndexOf('}', record);

            if (end < 0 || end > close)
                break;

            Apply(text[record..(end + 1)], entry);
        }

        at = close;

        return entry;
    }

    private static int SkipBlank(string text, int from)
    {
        for (int at = from; at < text.Length; ++at)
        {
            if (text[at] is not (' ' or '\t' or '\r' or '\n'))
                return at;
        }

        return -1;
    }
}
