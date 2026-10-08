using System.Globalization;

namespace FbxToMua.Core.Formats.FbxText;

public sealed class FbxTextNode
{
    public string Name { get; init; } = string.Empty;
    public List<string> Props { get; } = [];
    public List<int> PropAt { get; } = [];
    public List<double> Numbers { get; } = [];
    public List<int> Children { get; } = [];
}

public sealed class FbxTextTree
{
    private readonly List<FbxTextNode> _nodes = [];

    public int Root => 0;

    public FbxTextNode At(int index) => _nodes[index];

    public static FbxTextTree? Parse(byte[] data)
    {
        FbxTextTree tree = new();
        tree.Add(-1, "root");
        tree.Read(data);

        return tree._nodes.Count > 1 ? tree : null;
    }

    public int Find(int parent, string name)
    {
        if (parent < 0 || parent >= _nodes.Count)
            return -1;

        foreach (int child in _nodes[parent].Children)
        {
            if (_nodes[child].Name == name)
                return child;
        }

        return -1;
    }

    public List<int> All(int parent, string name)
    {
        if (parent < 0 || parent >= _nodes.Count)
            return [];

        return _nodes[parent].Children.Where(child => _nodes[child].Name == name).ToList();
    }

    public string Prop(int index, int slot)
    {
        if (index < 0 || index >= _nodes.Count || slot >= _nodes[index].Props.Count)
            return string.Empty;

        return _nodes[index].Props[slot];
    }

    private int Add(int parent, string name)
    {
        _nodes.Add(new FbxTextNode { Name = name });
        int index = _nodes.Count - 1;

        if (parent >= 0)
            _nodes[parent].Children.Add(index);

        return index;
    }

    private static bool IsNameByte(byte c) => char.IsAsciiLetterOrDigit((char)c) || c == '_' || c == '-';

    private static bool IsSpace(byte c) => c == ' ' || c == '\t' || c == '\r';

    private static bool Numeric(string text, out double value)
    {
        value = 0.0;

        return text.Length > 0 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static void Commit(FbxTextNode node, string token, bool quoted)
    {
        if (!quoted && Numeric(token, out double value))
        {
            node.Numbers.Add(value);
            return;
        }

        if (token.Length == 0 && !quoted)
            return;

        node.Props.Add(token);
        node.PropAt.Add(node.Numbers.Count);
    }

    private void Read(byte[] data)
    {
        Stack<int> stack = new([0]);
        int leaf = -1;
        int p = 0;

        while (p < data.Length)
        {
            int end = Array.IndexOf(data, (byte)'\n', p);
            end = end < 0 ? data.Length : end;
            int begin = p;
            p = end + 1;

            while (begin < end && IsSpace(data[begin]))
                ++begin;

            int stop = end;

            while (stop > begin && IsSpace(data[stop - 1]))
                --stop;

            if (begin >= stop || data[begin] == ';')
                continue;

            if (stop - begin == 1 && data[begin] == '}')
            {
                if (stack.Count > 1)
                    stack.Pop();

                leaf = -1;
                continue;
            }

            int colon = begin;

            while (colon < stop && IsNameByte(data[colon]))
                ++colon;

            bool header = colon > begin && colon < stop && data[colon] == ':';

            if (!header)
            {
                if (leaf >= 0)
                    ReadValues(data, begin, stop, _nodes[leaf]);

                continue;
            }

            int index = ReadHeader(data, begin, colon, stop, stack.Peek(), out bool opens);

            if (opens)
            {
                stack.Push(index);
                leaf = -1;
                continue;
            }

            leaf = index;
        }
    }

    private static void ReadValues(byte[] data, int begin, int stop, FbxTextNode node)
    {
        System.Text.StringBuilder token = new();

        for (int i = begin; i <= stop; ++i)
        {
            byte c = i < stop ? data[i] : (byte)',';

            if (c != ',')
            {
                if (!IsSpace(c))
                    token.Append((char)c);

                continue;
            }

            Commit(node, token.ToString(), false);
            token.Clear();
        }
    }

    private int ReadHeader(byte[] data, int begin, int colon, int stop, int parent, out bool opens)
    {
        string name = System.Text.Encoding.Latin1.GetString(data, begin, colon - begin);
        int rest = colon + 1;

        while (rest < stop && IsSpace(data[rest]))
            ++rest;

        opens = false;
        int tail = stop;

        if (tail > rest && data[tail - 1] == '{')
        {
            opens = true;
            --tail;

            while (tail > rest && IsSpace(data[tail - 1]))
                --tail;
        }

        int index = Add(parent, name);
        FbxTextNode node = _nodes[index];
        System.Text.StringBuilder token = new();
        bool quoted = false;
        bool wasQuoted = false;

        for (int i = rest; i <= tail; ++i)
        {
            byte c = i < tail ? data[i] : (byte)',';

            if (c == '"')
            {
                quoted = !quoted;
                wasQuoted = true;
                continue;
            }

            if (c == ',' && !quoted)
            {
                Commit(node, token.ToString(), wasQuoted);
                token.Clear();
                wasQuoted = false;
                continue;
            }

            if (quoted || !IsSpace(c))
                token.Append((char)c);
        }

        return index;
    }
}
