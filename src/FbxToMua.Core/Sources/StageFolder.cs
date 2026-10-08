using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Objects;

namespace FbxToMua.Core.Sources;

public static class StageFolder
{
    public const string ModelFile = "bg.fbx.bin";
    public const string ObjectFile = "object.txt";
    public const string NoteFile = "stage.txt";

    public static bool Holds(string folder) => File.Exists(Path.Combine(folder, ModelFile));

    public static ExportSource Load(string folder, string stage)
    {
        string notePath = Path.Combine(folder, NoteFile);
        Framing framing = File.Exists(notePath) ? StageText.Framing(Latin1(File.ReadAllBytes(notePath))) : Framing.Neutral;

        return Load(folder, stage, framing);
    }

    public static ExportSource Load(string folder, string stage, Framing framing)
    {
        byte[] objects = ReadOrEmpty(Path.Combine(folder, ObjectFile));
        string sheetFile = objects.Length == 0 ? string.Empty : ObjectList.SpriteFile(Latin1(objects));
        byte[] sheet = sheetFile.Length == 0 ? [] : ReadOrEmpty(Path.Combine(folder, sheetFile));

        return new ExportSource
        {
            Model = File.ReadAllBytes(Path.Combine(folder, ModelFile)),
            Image = name => ReadOrNull(Binary.DiskName.Path(folder, name)),
            Framing = framing,
            Stage = stage,
            Objects = sheet.Length == 0 ? [] : objects,
            Sheet = sheet,
            SheetName = Path.GetFileNameWithoutExtension(sheetFile),
        };
    }

    private static string Latin1(byte[] bytes) => System.Text.Encoding.Latin1.GetString(bytes);

    private static byte[] ReadOrEmpty(string path) => File.Exists(path) ? File.ReadAllBytes(path) : [];

    private static byte[]? ReadOrNull(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
}
