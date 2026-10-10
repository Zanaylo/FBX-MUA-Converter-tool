using FbxToMua.Core.Export;
using FbxToMua.Core.Import;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Workflows;

public sealed record ExportedStage(ExportResult Result, string Name);

public static class StageWorkflows
{
    public static ExportedStage Export(string folder, string? stage, string? name = null, Reframe? reframe = null)
    {
        if (StageFolder.Holds(folder))
        {
            string leaf = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
            string title = name ?? leaf;

            ExportSource loaded = StageFolder.Load(folder, StageNames.Stem(title));

            return new ExportedStage(StageExporter.Convert(loaded with { Reframe = reframe ?? Reframe.None }), title);
        }

        using IStageSource source = StageSources.Open(folder) ?? throw new StageConversionException($"no French-Bread stages found in {folder}");
        string chosen = stage ?? throw new StageConversionException("pick a stage to export");
        SourceStage? listed = source.Stages().FirstOrDefault(one => string.Equals(one.Folder, chosen, StringComparison.OrdinalIgnoreCase));
        string named = name ?? (listed is null || listed.Name.Length == 0 ? chosen : TextDisplay.Of(listed.Name));

        ExportSource read = StageSources.ExportSourceOf(source, chosen, StageNames.Stem(named, chosen.ToLowerInvariant()));

        return new ExportedStage(StageExporter.Convert(read with { Reframe = reframe ?? Reframe.None }), named);
    }

    public static ImStage Import(string folder, string stage, string? name = null)
    {
        ArcSource source = ArcSource.Open(folder) ?? throw new StageConversionException($"no BBTAG, BBCF or P4U2 stages found in {folder}");
        ArcStageResult result = source.Convert(stage) ?? throw new StageConversionException($"{stage} could not be read as a stage");

        string title = source.NameOf(stage);

        return new ImStage(name ?? title + ImStageFolder.Tag(source.Game), title, folder, stage, source.Game, result);
    }
}
