namespace FbxToMua.Core.Export;

public static class ExportFolder
{
    public const string SceneSuffix = ".pac";
    public const string GeometrySuffix = "_vtx.pac";
    public const string ArtSuffix = "_img.pac";

    private static readonly string[] Written = ["*.MUA", "*.mmot", "*.evb", "*.dds", "*.png", "*.tga", "*.bmp", "*.pac"];

    public static void Write(ExportResult result, string folder)
    {
        Directory.CreateDirectory(folder);
        Clear(folder);

        File.WriteAllBytes(Path.Combine(folder, result.Stage + StagePackager.ModelSuffix), result.Model);

        foreach (ExportFile file in result.Motions.Concat(result.Scripts).Concat(result.Images))
            File.WriteAllBytes(Binary.DiskName.Path(folder, file.Name), file.Data);

        foreach (ArcGame game in Enum.GetValues<ArcGame>())
        {
            string target = Path.Combine(folder, game.ToString().ToUpperInvariant());
            Directory.CreateDirectory(target);
            Clear(target);
            WriteArchives(target, result.Stage, StagePackager.Package(result, game, result.Stage));
        }
    }

    public static void WriteArchives(string folder, string stem, ExportArchives archives)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, stem + SceneSuffix), archives.Scene);
        File.WriteAllBytes(Path.Combine(folder, stem + GeometrySuffix), archives.Geometry);
        File.WriteAllBytes(Path.Combine(folder, stem + ArtSuffix), archives.Art);
    }

    private static void Clear(string folder)
    {
        foreach (string pattern in Written)
        {
            foreach (string file in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly))
                File.Delete(file);
        }
    }
}
