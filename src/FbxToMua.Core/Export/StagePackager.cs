using FbxToMua.Core.Formats.Fpac;

namespace FbxToMua.Core.Export;

public enum ArcGame
{
    Bbtag,
    Bbcf,
}

public static class StagePackager
{
    public const string ModelSuffix = ".MUA";
    public const string ModelFolder = "mdl.pac";
    public const string ScriptFolder = "scr.pac";
    public const string MotionFolder = "mot.pac";
    public const string CameraFolder = "cammot.pac";

    public static ExportArchives Package(ExportResult result, ArcGame game, string model)
    {
        string modelFile = model + ModelSuffix;
        List<FpacEntry> scene =
        [
            new(ModelFolder, Folder([new ExportFile(modelFile, result.Bare)])),
            new(ScriptFolder, Folder(result.Scripts)),
            new(MotionFolder, Folder(result.Motions)),
        ];

        if (game == ArcGame.Bbcf)
            scene.Add(new FpacEntry(CameraFolder, Folder([new ExportFile(IntroCamera.FileName(model), IntroCamera.Still(model))])));

        ExportArchives plain = new(Fpac.Build(scene), Folder([new ExportFile(modelFile, result.Model)]), Folder(result.Images));

        if (game != ArcGame.Bbcf)
            return plain;

        return new ExportArchives(Fpac.Pack(plain.Scene), Fpac.Pack(plain.Geometry), Fpac.Pack(plain.Art));
    }

    private static byte[] Folder(IEnumerable<ExportFile> files)
    {
        List<FpacEntry> entries = files.Select(file => new FpacEntry(file.Name, file.Data))
            .OrderBy(entry => entry.Name.ToLowerInvariant(), StringComparer.Ordinal)
            .ToList();

        return Fpac.Build(entries);
    }
}
