namespace FbxToMua.Cli.Commands;

public sealed class Arguments
{
    private const string OptionPrefix = "--";

    private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _switches = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Positional { get; } = [];

    public Arguments(IReadOnlyList<string> raw)
    {
        for (int i = 0; i < raw.Count; ++i)
        {
            if (!raw[i].StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                Positional.Add(raw[i]);
                continue;
            }

            string name = raw[i][OptionPrefix.Length..];

            if (i + 1 < raw.Count && !raw[i + 1].StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                _options[name] = raw[++i];
                continue;
            }

            _switches.Add(name);
        }
    }

    public string? Option(string name) => _options.GetValueOrDefault(name);

    public bool Switch(string name) => _switches.Contains(name);

    public string? At(int index) => index < Positional.Count ? Positional[index] : null;
}
