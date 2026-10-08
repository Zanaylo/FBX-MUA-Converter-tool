using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Fpac;

namespace FbxToMua.Core.Install;

public static class InstallRules
{
    private static readonly string[] SharedEntries = [StagePackager.ModelFolder, StagePackager.ScriptFolder];
    private static readonly string[] BbcfEntries = [StagePackager.MotionFolder, StagePackager.CameraFolder];

    public static string ModelName(byte[] scene)
    {
        foreach (string path in Fpac.Walk(scene).Keys)
        {
            string leaf = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];

            if (leaf.Length > StagePackager.ModelSuffix.Length && leaf.EndsWith(StagePackager.ModelSuffix, StringComparison.OrdinalIgnoreCase))
                return leaf[..^StagePackager.ModelSuffix.Length];
        }

        return string.Empty;
    }

    public static bool Loadable(ArcGame game, byte[] scene)
    {
        IReadOnlyList<string> names = Fpac.Names(scene);

        if (!HoldsAll(names, SharedEntries))
            return false;

        return game != ArcGame.Bbcf || HoldsAll(names, BbcfEntries);
    }

    private static bool HoldsAll(IReadOnlyList<string> names, string[] wanted)
    {
        return wanted.All(one => names.Any(name => string.Equals(name, one, StringComparison.OrdinalIgnoreCase)));
    }
}
