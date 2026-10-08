using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Objects;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Sources;

public static class StageSources
{
    public static IStageSource? Open(string folder)
    {
        IStageSource? source = OpenGame(folder);

        return source is null ? null : new EnglishNamedSource(source);
    }

    private static IStageSource? OpenGame(string folder)
    {
        GameKind game = GameFolder.Detect(folder);

        return game switch
        {
            GameKind.Uni2 => Uni2Source.Open(folder),
            GameKind.Uni => UniSource.Open(folder, game),
            GameKind.Mbtl => MbtlSource.Open(folder),
            GameKind.Mbaa => MbaaSource.Open(folder),
            GameKind.Dfci => Opened(new DfciSource(folder)),
            GameKind.Uniel => Opened(new UnielSource(folder)),
            _ => null,
        };
    }

    public static ExportSource ExportSourceOf(IStageSource source, string stage, string name)
    {
        byte[] model = source.Read(stage, StageFolder.ModelFile) ?? throw new StageConversionException($"{stage} has no {StageFolder.ModelFile}");
        byte[] objects = source.Read(stage, StageFolder.ObjectFile) ?? [];
        string sheetFile = objects.Length == 0 ? string.Empty : ObjectList.SpriteFile(System.Text.Encoding.Latin1.GetString(objects));
        byte[] sheet = sheetFile.Length == 0 ? [] : source.Read(stage, sheetFile) ?? [];

        return new ExportSource
        {
            Model = model,
            Image = file => source.Read(stage, file),
            Framing = FramingOf(source.BgList(), stage),
            Stage = name,
            Objects = sheet.Length == 0 ? [] : objects,
            Sheet = sheet,
            SheetName = Path.GetFileNameWithoutExtension(sheetFile),
        };
    }

    public static Framing FramingOf(string bgList, string stage)
    {
        Framing framing = Framing.Neutral;
        string? block = BgListText.Block(bgList, stage);

        if (block is null)
            return framing;

        if (StageText.TryTriple(BgListText.Field(block, "Scale") ?? string.Empty, out Float3 scale))
            framing.Scale = scale;

        if (StageText.TryTriple(BgListText.Field(block, "Position") ?? string.Empty, out Float3 position))
            framing.Position = position;

        if (StageText.TrySingle(BgListText.Field(block, "ViewRotationX") ?? string.Empty, out float tilt))
            framing.Tilt = tilt;

        if (StageText.TrySingle(BgListText.Field(block, "ViewRotationY") ?? string.Empty, out float turn))
            framing.Turn = turn;

        return framing;
    }

    private static IStageSource? Opened(FolderSource source) => source.IsOpen ? source : null;
}
