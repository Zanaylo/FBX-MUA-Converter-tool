using FbxToMua.Core.Export;
using FbxToMua.Core.Sources;
using FbxToMua.Core.Sources.Tables;

namespace FbxToMua.Core.Install;

public sealed record StageTarget(string Group, string Stem)
{
    public string Relative => $"data/bg/{Group}/{Stem}";

    public string Key => $"{Group}/{Stem}";
}

public enum ArchivePart
{
    Scene,
    Geometry,
    Art,
}

public interface IStageStore
{
    IReadOnlyList<StageTarget> Targets();

    byte[]? ReadRaw(StageTarget target, ArchivePart part);

    byte[]? ReadPlain(StageTarget target, ArchivePart part);

    byte[] Plain(StageTarget target, ArchivePart part, byte[] raw);

    void WriteRaw(StageTarget target, ArchivePart part, byte[] raw);

    void WritePlain(StageTarget target, ArchivePart part, byte[] plain);
}

public static class ArchiveParts
{
    public static readonly ArchivePart[] All = [ArchivePart.Scene, ArchivePart.Geometry, ArchivePart.Art];

    public static string Suffix(ArchivePart part)
    {
        return part switch
        {
            ArchivePart.Geometry => ExportFolder.GeometrySuffix,
            ArchivePart.Art => ExportFolder.ArtSuffix,
            _ => ExportFolder.SceneSuffix,
        };
    }

    public static byte[] Of(ExportArchives archives, ArchivePart part)
    {
        return part switch
        {
            ArchivePart.Geometry => archives.Geometry,
            ArchivePart.Art => archives.Art,
            _ => archives.Scene,
        };
    }
}

public sealed class LooseStageStore(string gameFolder) : IStageStore
{
    private const string GroupPrefix = "main";
    private const string StagePrefix = "bg_";

    private string Stages => Path.Combine(gameFolder, "data", "bg");

    public IReadOnlyList<StageTarget> Targets()
    {
        if (!Directory.Exists(Stages))
            return [];

        List<StageTarget> targets = [];

        foreach (string group in Directory.EnumerateDirectories(Stages, GroupPrefix + "*").Order(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string scene in Directory.EnumerateFiles(group, StagePrefix + "*" + ExportFolder.SceneSuffix).Order(StringComparer.OrdinalIgnoreCase))
            {
                string leaf = Path.GetFileName(scene);

                if (leaf.EndsWith(ExportFolder.GeometrySuffix, StringComparison.OrdinalIgnoreCase) || leaf.EndsWith(ExportFolder.ArtSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                StageTarget target = new(Path.GetFileName(group), leaf[..^ExportFolder.SceneSuffix.Length]);

                if (ArchiveParts.All.All(part => File.Exists(PathOf(target, part))))
                    targets.Add(target);
            }
        }

        return targets;
    }

    public byte[]? ReadRaw(StageTarget target, ArchivePart part)
    {
        string path = PathOf(target, part);

        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public byte[]? ReadPlain(StageTarget target, ArchivePart part) => ReadRaw(target, part);

    public byte[] Plain(StageTarget target, ArchivePart part, byte[] raw) => raw;

    public void WriteRaw(StageTarget target, ArchivePart part, byte[] raw) => File.WriteAllBytes(PathOf(target, part), raw);

    public void WritePlain(StageTarget target, ArchivePart part, byte[] plain) => WriteRaw(target, part, plain);

    private string PathOf(StageTarget target, ArchivePart part) => Path.Combine(Stages, target.Group, target.Stem + ArchiveParts.Suffix(part));
}

public sealed class HashedStageStore : IStageStore
{
    private const string AssetFolder = "asset";
    private const string GroupPrefix = "main";

    private readonly string _assets;

    public HashedStageStore(string gameFolder) => _assets = Path.Combine(gameFolder, AssetFolder);

    public static bool Holds(string gameFolder) => Directory.Exists(Path.Combine(gameFolder, AssetFolder));

    public IReadOnlyList<StageTarget> Targets()
    {
        List<StageTarget> targets = [];

        foreach (string stem in BbtagStagePaths.Stages)
        {
            int slash = stem.IndexOf('/');
            StageTarget target = new(stem[..slash], stem[(slash + 1)..]);

            if (target.Group.StartsWith(GroupPrefix, StringComparison.OrdinalIgnoreCase) && ArchiveParts.All.All(part => File.Exists(PathOf(target, part))))
                targets.Add(target);
        }

        return targets;
    }

    public byte[]? ReadRaw(StageTarget target, ArchivePart part)
    {
        string path = PathOf(target, part);

        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public byte[]? ReadPlain(StageTarget target, ArchivePart part)
    {
        byte[]? raw = ReadRaw(target, part);

        return raw is null ? null : Plain(target, part, raw);
    }

    public byte[] Plain(StageTarget target, ArchivePart part, byte[] raw)
    {
        byte[] plain = (byte[])raw.Clone();
        ArcCrypt.Apply(ArcKey.Bbtag, Relative(target, part), plain);

        return plain;
    }

    public void WriteRaw(StageTarget target, ArchivePart part, byte[] raw) => File.WriteAllBytes(PathOf(target, part), raw);

    public void WritePlain(StageTarget target, ArchivePart part, byte[] plain) => WriteRaw(target, part, Plain(target, part, plain));

    private static string Relative(StageTarget target, ArchivePart part) => target.Relative + ArchiveParts.Suffix(part);

    private string PathOf(StageTarget target, ArchivePart part) => Path.Combine(_assets, ArcCrypt.NameOf(Relative(target, part)));
}

public static class StageStores
{
    public static IStageStore? For(ArcGame game, string gameFolder)
    {
        if (game == ArcGame.Bbtag && HashedStageStore.Holds(gameFolder) && !Directory.Exists(Path.Combine(gameFolder, "data", "bg")))
            return new HashedStageStore(gameFolder);

        return Directory.Exists(Path.Combine(gameFolder, "data", "bg")) ? new LooseStageStore(gameFolder) : null;
    }
}
