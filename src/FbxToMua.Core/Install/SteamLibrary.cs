using System.Runtime.Versioning;
using Microsoft.Win32;

namespace FbxToMua.Core.Install;

public interface ISteamLibrary
{
    IReadOnlyList<string> Folders();

    string? GameFolder(string installFolder);
}

public sealed class SteamLibrary : ISteamLibrary
{
    private const string SteamKey = @"Software\Valve\Steam";
    private const string SteamPathValue = "SteamPath";
    private const string LibraryFile = @"steamapps\libraryfolders.vdf";
    private const string CommonFolder = @"steamapps\common";
    private const string PathKey = "\"path\"";

    public IReadOnlyList<string> Folders()
    {
        string? steam = SteamPath();

        if (steam is null)
            return [];

        List<string> folders = [steam];
        string library = Path.Combine(steam, LibraryFile);

        if (!File.Exists(library))
            return folders;

        string text = File.ReadAllText(library);

        for (int at = text.IndexOf(PathKey, StringComparison.Ordinal); at >= 0; at = text.IndexOf(PathKey, at + 1, StringComparison.Ordinal))
        {
            string? folder = QuotedAfter(text, at + PathKey.Length);

            if (folder is not null && !folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                folders.Add(folder);
        }

        return folders;
    }

    public string? GameFolder(string installFolder)
    {
        return Folders().Select(library => Path.Combine(library, CommonFolder, installFolder)).FirstOrDefault(Directory.Exists);
    }

    private static string? SteamPath()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        return ReadRegistry()?.Replace('/', '\\');
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadRegistry()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(SteamKey);

        return key?.GetValue(SteamPathValue) as string;
    }

    private static string? QuotedAfter(string text, int from)
    {
        int open = text.IndexOf('"', from);

        if (open < 0)
            return null;

        System.Text.StringBuilder value = new();

        for (int at = open + 1; at < text.Length; ++at)
        {
            if (text[at] == '"')
                return value.ToString();

            if (text[at] == '\\' && at + 1 < text.Length)
                ++at;

            value.Append(text[at]);
        }

        return null;
    }
}
