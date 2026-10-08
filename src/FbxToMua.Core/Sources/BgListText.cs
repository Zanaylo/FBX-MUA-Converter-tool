namespace FbxToMua.Core.Sources;

public readonly record struct BgPair(string Key, string Value);

public static class BgListText
{
    public static bool LeadByte(char letter) => letter is (>= '\u0081' and <= '\u009f') or (>= 'à' and <= 'ü');

    public static int MatchPair(string text, int open)
    {
        char opener = open < text.Length ? text[open] : '\0';
        char closer = opener switch { '{' => '}', '[' => ']', _ => '\0' };

        if (closer == '\0')
            return -1;

        int depth = 0;

        for (int at = open; at < text.Length; ++at)
        {
            if (LeadByte(text[at]) && at + 1 < text.Length)
            {
                ++at;
                continue;
            }

            depth += text[at] == opener ? 1 : text[at] == closer ? -1 : 0;

            if (depth == 0)
                return at + 1;
        }

        return -1;
    }

    public static string? Block(string bgList, string stage)
    {
        string named = $"\"{stage}\"";
        int number = NumberOf(stage);

        for (int pass = 0; pass < 2; ++pass)
        {
            if (pass == 1 && number < 0)
                return null;

            for (int at = bgList.IndexOf("Bg_", StringComparison.Ordinal); at >= 0; at = bgList.IndexOf("Bg_", at + 3, StringComparison.Ordinal))
            {
                int digits = at + 3;

                while (digits < bgList.Length && char.IsAsciiDigit(bgList[digits]))
                    ++digits;

                if (digits == at + 3)
                    continue;

                if (pass == 1 && Formats.CText.Atoi(bgList[(at + 3)..digits]) != number)
                    continue;

                int open = OpenBrace(bgList, digits);

                if (open < 0)
                    continue;

                int end = MatchPair(bgList, open);

                if (end < 0)
                    return null;

                string body = bgList.Substring(open + 1, end - open - 2);

                if (pass == 0 && Field(body, "DataFile") != named)
                    continue;

                return body;
            }
        }

        return null;
    }

    public static string? Field(string block, string key)
    {
        for (int at = block.IndexOf(key, StringComparison.Ordinal); at >= 0; at = block.IndexOf(key, at + 1, StringComparison.Ordinal))
        {
            if (!KeyAt(block, at, key))
                continue;

            int equals = Skip(block, at + key.Length, " \t");

            if (equals >= block.Length || block[equals] != '=')
                continue;

            int valueAt = ValueStart(block, equals + 1);
            int valueEnd = ValueEnd(block, valueAt);

            if (valueEnd < 0)
                return null;

            while (valueEnd > valueAt && block[valueEnd - 1] is ' ' or '\t')
                --valueEnd;

            return valueEnd > valueAt ? block[valueAt..valueEnd] : null;
        }

        return null;
    }

    public static string Unquoted(string value)
    {
        if (value.Length == 0 || value[0] != '"')
            return value;

        return value.Substring(1, value.Length - (value.Length > 1 && value[^1] == '"' ? 2 : 1));
    }

    public static List<BgPair> Pairs(string block)
    {
        List<BgPair> pairs = [];

        for (int at = 0; at < block.Length; ++at)
        {
            if (LeadByte(block[at]))
            {
                ++at;
                continue;
            }

            int skipped = SkipComment(block, at);

            if (skipped != at)
            {
                at = skipped;
                continue;
            }

            if (block[at] is '{' or '[')
            {
                int close = MatchPair(block, at);

                if (close < 0)
                    return pairs;

                at = close - 1;
                continue;
            }

            if (!IsKeyStart(block, at))
                continue;

            int end = at;

            while (end < block.Length && IsKeyByte(block[end]))
                ++end;

            int equals = Skip(block, end, " \t");

            if (equals >= block.Length || block[equals] != '=')
            {
                at = end - 1;
                continue;
            }

            int valueAt = PairStart(block, equals + 1);
            int valueEnd = PairEnd(block, valueAt);

            if (valueEnd < 0)
                return pairs;

            if (valueEnd <= valueAt)
            {
                at = valueAt - 1;
                continue;
            }

            pairs.Add(new BgPair(block[at..end], block[valueAt..valueEnd]));
            at = valueEnd - 1;
        }

        return pairs;
    }

    public static int NumberOf(string stage)
    {
        int at = stage.Length;

        while (at > 0 && char.IsAsciiDigit(stage[at - 1]))
            --at;

        return at == stage.Length ? -1 : Formats.CText.Atoi(stage[at..]);
    }

    private static int OpenBrace(string bgList, int from)
    {
        int open = from;
        int equals = 0;

        while (open < bgList.Length && bgList[open] != '{')
        {
            char c = bgList[open];

            if (c == '=')
                ++equals;
            else if (c is not (' ' or '\t' or '\r' or '\n'))
                return -1;

            ++open;
        }

        return open < bgList.Length && equals == 1 ? open : -1;
    }

    private static int Skip(string text, int at, string of)
    {
        while (at < text.Length && of.Contains(text[at]) && text[at] != '\0')
            ++at;

        return at;
    }

    private static int ValueStart(string block, int after)
    {
        int sameLine = Skip(block, after, " \t");
        int anyLine = Skip(block, after, " \t\r\n");

        return anyLine < block.Length && block[anyLine] == '[' ? anyLine : sameLine;
    }

    private static int ValueEnd(string block, int value)
    {
        if (value < block.Length && block[value] == '[')
            return MatchPair(block, value);

        int end = value;

        while (end < block.Length && block[end] != ',' && block[end] != '\n' && block[end] != '\r'
            && !(block[end] == '/' && end + 1 < block.Length && block[end + 1] == '/'))
        {
            ++end;
        }

        return end;
    }

    private static bool KeyAt(string text, int at, string key)
    {
        if (at + key.Length > text.Length || string.CompareOrdinal(text, at, key, 0, key.Length) != 0)
            return false;

        char before = at == 0 ? ' ' : text[at - 1];
        char after = at + key.Length >= text.Length ? ' ' : text[at + key.Length];

        return !char.IsAsciiLetterOrDigit(before) && before != '_' && !char.IsAsciiLetterOrDigit(after) && after != '_';
    }

    private static int SkipComment(string text, int at)
    {
        if (text[at] == '"')
        {
            int close = text.IndexOfAny(['"', '\n'], at + 1);
            return close < 0 ? text.Length : close;
        }

        if (text[at] != '/' || at + 1 >= text.Length)
            return at;

        if (text[at + 1] == '/')
        {
            int line = text.IndexOf('\n', at);
            return line < 0 ? text.Length : line;
        }

        if (text[at + 1] != '*')
            return at;

        int end = text.IndexOf("*/", at + 2, StringComparison.Ordinal);
        return end < 0 ? text.Length : end + 1;
    }

    private static bool IsKeyByte(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    private static bool IsKeyStart(string text, int at) => IsKeyByte(text[at]) && !char.IsAsciiDigit(text[at]) && (at == 0 || !IsKeyByte(text[at - 1]));

    private static int PairStart(string block, int after)
    {
        int anyLine = Skip(block, after, " \t\r\n");

        if (anyLine < block.Length && block[anyLine] is '[' or '{')
            return anyLine;

        return Skip(block, after, " \t");
    }

    private static int PairEnd(string block, int value)
    {
        if (value >= block.Length)
            return value;

        if (block[value] == '{')
            return MatchPair(block, value);

        if (block[value] == '"')
        {
            int close = block.IndexOf('"', value + 1);
            return close < 0 ? -1 : close + 1;
        }

        int end = ValueEnd(block, value);

        while (end > value && block[end - 1] is ' ' or '\t')
            --end;

        return end;
    }
}
