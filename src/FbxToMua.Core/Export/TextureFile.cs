namespace FbxToMua.Core.Export;

public static class TextureFile
{
    public const string Extension = ".dds";
    private const string Space = " ";

    public static List<string> Candidates(string leaf)
    {
        string dds = Path.ChangeExtension(leaf, Extension);

        return new[] { dds, dds.Replace(Space, string.Empty), leaf }.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
