using FbxToMua.Core.Sources;

namespace FbxToMua.Cli.Commands;

public sealed class StagesCommand : ICommand
{
    public string Name => "stages";

    public string Usage => "stages <game folder>";

    public int Run(Arguments arguments)
    {
        string? folder = arguments.At(0);

        if (folder is null)
            return Report.Usage(this);

        GameKind game = GameFolder.Detect(folder);
        IReadOnlyList<SourceStage>? stages = GameFolder.IsArcSys(game) ? ArcSource.Open(folder)?.Stages() : FrenchBread(folder);

        if (stages is null)
            return Report.Failure($"No stages found in {folder}.");

        Console.WriteLine($"{GameFolder.Title(game)}:");

        foreach (SourceStage stage in stages)
            Console.WriteLine($"  {stage.Folder,-24} {TextDisplay.Of(stage.Name)}");

        return Report.Ok;
    }

    private static IReadOnlyList<SourceStage>? FrenchBread(string folder)
    {
        using IStageSource? source = StageSources.Open(folder);

        return source?.Stages();
    }
}
